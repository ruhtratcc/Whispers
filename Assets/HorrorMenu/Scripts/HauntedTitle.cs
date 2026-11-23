// Coordena o efeito do título na apresentação do menu.

using UnityEngine;
using TMPro;

namespace SchoolMenu
{
    // Glyph changes are deterministic; this never shakes the whole title.
    public sealed class HauntedTitle : MonoBehaviour
    {
        [SerializeField] public TMP_Text title;
        [SerializeField] float unevenBaseline = 2.2f;
        [SerializeField] float maximumTilt = 1.4f;
        [SerializeField] float maximumStretch = .085f;
        [SerializeField] bool briefFlicker = true;
        [SerializeField, Range(.5f, 1)] float flickerAlpha = .82f;
        float nextFlicker, flickerUntil;
        bool dimming;

        void OnEnable()
        {
            if (!title) return;
            title.OnPreRenderText += WeatherGlyphs;
            nextFlicker = Time.unscaledTime + Random.Range(11f, 21f);
            dimming = false;
            title.ForceMeshUpdate();
        }

        void OnDisable()
        {
            if (title) title.OnPreRenderText -= WeatherGlyphs;
        }

        void Update()
        {
            if (!title || !briefFlicker) return;
            float time = Time.unscaledTime;
            if (time >= nextFlicker)
            {
                dimming = true;
                flickerUntil = time + .10f;
                nextFlicker = time + Random.Range(11f, 21f);
                title.ForceMeshUpdate();
            }
            else if (dimming && time >= flickerUntil)
            {
                dimming = false;
                title.ForceMeshUpdate();
            }
        }

        void WeatherGlyphs(TMP_TextInfo info)
        {
            for (int i = 0; i < info.characterCount; i++)
            {
                var character = info.characterInfo[i];
                if (!character.isVisible) continue;
                var vertices = info.meshInfo[character.materialReferenceIndex].vertices;
                int index = character.vertexIndex;
                if (dimming)
                {
                    var colors = info.meshInfo[character.materialReferenceIndex].colors32;
                    for (int corner = 0; corner < 4; corner++)
                        colors[index + corner].a = (byte)(colors[index + corner].a * flickerAlpha);
                }
                Vector3 center = (vertices[index] + vertices[index + 2]) * .5f;
                float sample = Mathf.Sin(i * 12.9898f + 4.7f);
                var rotation = Quaternion.Euler(0, 0, sample * maximumTilt);
                var stretch = new Vector3(.97f, 1f + Mathf.Abs(sample) * maximumStretch, 1f);
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 offset = vertices[index + corner] - center;
                    vertices[index + corner] = center + rotation * Vector3.Scale(offset, stretch)
                        + Vector3.up * sample * unevenBaseline;
                }
            }
        }
    }
}

