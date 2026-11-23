// Controla o brilho usado como indicação do grampo.

using UnityEngine;

namespace HorrorGame
{
    public sealed class PinGlint : MonoBehaviour
    {
        Renderer[] surfaces;
        MaterialPropertyBlock[] originals, blocks;
        GameSession game;
        LockpickPickup pickup;
        void Awake()
        {
            game = GameSession.Instance;
            pickup = GetComponent<LockpickPickup>();
            surfaces = GetComponentsInChildren<Renderer>(true);
            originals = new MaterialPropertyBlock[surfaces.Length];
            blocks = new MaterialPropertyBlock[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++)
            {
                originals[i] = new MaterialPropertyBlock();
                blocks[i] = new MaterialPropertyBlock();
                surfaces[i].GetPropertyBlock(originals[i]);
            }
        }

        void LateUpdate()
        {
            bool lit = game && game.CanControl && game.player && game.save && game.save.flashlight && game.save.flashlight.IsOn && pickup && pickup.CanInteract(game.interaction) && Vector3.Distance(game.player.transform.position, transform.position) < 3.8f;
            for (int i = 0; i < surfaces.Length; i++)
                if (surfaces[i])
                {
                    if (!lit)
                    {
                        surfaces[i].SetPropertyBlock(originals[i]);
                        continue;
                    }

                    surfaces[i].GetPropertyBlock(blocks[i]);
                    blocks[i].SetFloat("_Smoothness", .45f + .22f * Mathf.Sin(Time.time * Mathf.PI));
                    surfaces[i].SetPropertyBlock(blocks[i]);
                }
        }

        void OnDisable()
        {
            if (surfaces != null)
                for (int i = 0; i < surfaces.Length; i++)
                    if (surfaces[i])
                        surfaces[i].SetPropertyBlock(originals[i]);
        }
    }
}
