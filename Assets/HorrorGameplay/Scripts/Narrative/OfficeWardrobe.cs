// Controla a interação com o armário do escritório.

using System;
using UnityEngine;

namespace HorrorGame
{
    public sealed class OfficeWardrobe : PersistentObject
    {
        public BeforeDemolitionOfficeFlow office;
        public Transform hinge, rightHinge;
        public SoundSet creak;
        public Light interiorLight;
        [Min(.1f)]
        public float seconds = 1.3f;
        public float openAngle = -94, ajarAngle = -18;
        public bool Open { get; private set; }

        Quaternion leftClosed, rightClosed;
        [Serializable]
        sealed class Data
        {
            public bool open;
        }

        bool Unlocked => office && office.inbox && office.inbox.State != null && office.inbox.State.IsRead("email.07");

        void Awake()
        {
            if (string.IsNullOrEmpty(persistentId))
                persistentId = "bd.office.wardrobe.presentation";
            if (!office)
                office = GetComponent<BeforeDemolitionOfficeFlow>();
            if (hinge)
                leftClosed = hinge.localRotation;
            if (rightHinge)
                rightClosed = rightHinge.localRotation;
        }

        public void Reconcile()
        {
            if (!Unlocked)
                Open = false;
        }

        public bool Toggle()
        {
            if (!Unlocked)
                return false;
            Open = !Open;
            if (Open)
                office.GetComponent<ContextBarks>()?.Enqueue("wardrobe");
            AudioManager.Instance?.Play(creak, hinge ? hinge.position : transform.position, .10f, AudioChannel.SFX, true);
            return true;
        }

        void Update()
        {
            Reconcile();
            if (interiorLight)
                interiorLight.intensity = Unlocked ? (Open ? .07f : .025f) : 0;
            float angle = Unlocked ? (Open ? openAngle : ajarAngle) : 0;
            float follow = 1 - Mathf.Exp(-Time.deltaTime * 5 / Mathf.Max(.1f, seconds));
            if (hinge)
                hinge.localRotation = Quaternion.Slerp(hinge.localRotation, leftClosed * Quaternion.Euler(0, angle, 0), follow);
            if (rightHinge)
                rightHinge.localRotation = Quaternion.Slerp(rightHinge.localRotation, rightClosed * Quaternion.Euler(0, -angle, 0), follow);
        }

        public override string CaptureState() => JsonUtility.ToJson(new Data { open = Open });
        public override void RestoreState(string json)
        {
            var d = JsonUtility.FromJson<Data>(json);
            Open = d != null && d.open;
        }
    }
}
