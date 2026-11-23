// Contém lógica de inimigo de protótipo, separada da campanha psicológica atual.

using UnityEngine;
using UnityEngine.AI;

namespace HorrorGame
{
    public enum EnemyState
    {
        Patrol,
        Investigate,
        Chase,
        Search
    }

    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyBrain : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        public FirstPersonMotor player;
        [SerializeField]
        public Transform eyes;
        [SerializeField]
        public Transform[] patrolPoints;
        [Header("Vision and hearing")]
        [SerializeField]
        public LayerMask visionMask = ~0;
        [SerializeField]
        float visionDistance = 10, fieldOfView = 85, hearingGain = 1, occludedHearing = .45f, senseInterval = .15f;
        [Header("Movement")]
        [SerializeField]
        float patrolSpeed = 1.3f, chaseSpeed = 3.7f, investigateSpeed = 2, loseSightSeconds = 2.3f;
        [SerializeField]
        float searchSeconds = 8, searchRadius = 3, attackDistance = 1.15f;
        [Header("Enemy footsteps")]
        [SerializeField]
        public SoundSet footsteps;
        [SerializeField]
        float stride = 1.25f;
        public EnemyState State { get; private set; } = EnemyState.Patrol;
        public bool VisibleNow { get; private set; }
        public Vector3 LastKnownPosition => lastKnown;

        NavMeshAgent agent;
        int patrolIndex;
        float nextSense, lastSeen = -100, searchUntil, nextSearch, stepDistance;
        Vector3 lastKnown, previousPosition;
        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            previousPosition = transform.position;
        }

        void OnEnable() => NoiseSystem.Emitted += Hear;
        void OnDisable() => NoiseSystem.Emitted -= Hear;
        void Start()
        {
            if (!player && GameSession.Instance)
                player = GameSession.Instance.player;
            if (!eyes)
                eyes = transform;
            if (!agent.isOnNavMesh)
            {
                Debug.LogWarning("Enemy needs a baked NavMesh matching its Agent Type.", this);
                enabled = false;
                return;
            }

            Patrol();
        }

        void Update()
        {
            if (!player || !agent.isOnNavMesh)
                return;
            if (GameSession.Instance && GameSession.Instance.State != GameState.Playing)
            {
                agent.isStopped = true;
                return;
            }

            agent.isStopped = false;
            if (Time.time >= nextSense)
            {
                nextSense = Time.time + senseInterval;
                VisibleNow = CanSeePlayer();
                if (VisibleNow)
                {
                    lastSeen = Time.time;
                    lastKnown = player.transform.position;
                    Change(EnemyState.Chase);
                    GameSession.Instance?.atmosphere?.AddTension(.09f);
                }
            }

            if (State == EnemyState.Chase)
            {
                Destination(lastKnown);
                if (Time.time - lastSeen > loseSightSeconds)
                    BeginSearch();
                else if (!player.IsHidden && Vector3.Distance(transform.position, player.transform.position) < attackDistance && LineToPlayer())
                    GameSession.Instance?.Kill();
            }
            else if (State == EnemyState.Investigate)
            {
                if (Arrived())
                    BeginSearch();
            }
            else if (State == EnemyState.Search)
            {
                if (Time.time >= searchUntil)
                    Patrol();
                else if (Time.time >= nextSearch || Arrived())
                {
                    nextSearch = Time.time + Random.Range(1.8f, 3);
                    var circle = Random.insideUnitCircle * searchRadius;
                    Destination(lastKnown + new Vector3(circle.x, 0, circle.y));
                }
            }
            else if (Arrived() && patrolPoints != null && patrolPoints.Length > 0)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                Destination(patrolPoints[patrolIndex].position);
            }

            // A pursuer who heard a gasp can inspect the hiding location; holding breath suppresses discovery.
            if (player.IsHidden && !player.HoldingBreath && State != EnemyState.Patrol && Vector3.Distance(lastKnown, player.transform.position) < 2 && Vector3.Distance(transform.position, player.transform.position) < 1.4f)
                GameSession.Instance?.Kill();
            float moved = Vector3.Distance(transform.position, previousPosition);
            previousPosition = transform.position;
            if (moved < 2)
                stepDistance += moved;
            if (stepDistance >= stride)
            {
                stepDistance %= stride;
                AudioManager.Instance?.Play(footsteps, transform.position, .35f);
            }
        }

        bool LineToPlayer()
        {
            Vector3 origin = eyes.position;
            Vector3 target = player.transform.position + Vector3.up * Mathf.Min(1.4f, player.EyeHeight);
            Vector3 delta = target - origin;
            if (Physics.Raycast(origin, delta.normalized, out var hit, delta.magnitude + .05f, visionMask, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<FirstPersonMotor>() == player;
            return false;
        }

        public bool CanSeePlayer()
        {
            if (!player || player.IsHidden)
                return false;
            Vector3 delta = player.transform.position + Vector3.up * player.EyeHeight - eyes.position;
            float distance = visionDistance * (player.IsCrouched ? .7f : 1);
            if (delta.magnitude > distance || Vector3.Angle(eyes.forward, delta) > fieldOfView * .5f)
                return false;
            return LineToPlayer();
        }

        void Hear(NoiseEvent noise)
        {
            if (!enabled || !agent || !agent.isOnNavMesh || noise.source == gameObject || noise.source && noise.source.transform.IsChildOf(transform))
                return;
            if (GameSession.Instance && GameSession.Instance.State != GameState.Playing)
                return;
            if (State == EnemyState.Chase && VisibleNow)
                return;
            float radius = noise.radius * hearingGain;
            if (Physics.Linecast(eyes ? eyes.position : transform.position + Vector3.up, noise.position + Vector3.up * .1f, out var hit, visionMask, QueryTriggerInteraction.Ignore))
            {
                if (!noise.source || !hit.transform.IsChildOf(noise.source.transform))
                    radius *= occludedHearing;
            }

            if (Vector3.Distance(transform.position, noise.position) > radius)
                return;
            lastKnown = noise.position;
            Change(EnemyState.Investigate);
            Destination(lastKnown);
        }

        void Change(EnemyState next)
        {
            State = next;
            agent.speed = next == EnemyState.Chase ? chaseSpeed : next == EnemyState.Patrol ? patrolSpeed : investigateSpeed;
        }

        void BeginSearch()
        {
            Change(EnemyState.Search);
            searchUntil = Time.time + searchSeconds;
            nextSearch = 0;
        }

        void Patrol()
        {
            Change(EnemyState.Patrol);
            if (patrolPoints != null && patrolPoints.Length > 0 && patrolPoints[patrolIndex])
                Destination(patrolPoints[patrolIndex].position);
        }

        bool Arrived() => !agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + .2f || agent.pathStatus == NavMeshPathStatus.PathInvalid);
        void Destination(Vector3 point)
        {
            if (NavMesh.SamplePosition(point, out var hit, 2, agent.areaMask))
                agent.SetDestination(hit.position);
        }
    }
}
