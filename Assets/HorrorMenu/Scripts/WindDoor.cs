// Anima a porta de fundo do menu.

using UnityEngine;

namespace SchoolMenu
{
    public sealed class WindDoor : MonoBehaviour
    {
        [SerializeField] float restingAngle = -34f;
        [SerializeField] float swingAngle = 6f;
        [SerializeField] float windSpeed = .42f;
        [SerializeField] float gustAngle = 1.2f;

        void Update()
        {
            float time = Time.unscaledTime;
            float breeze = Mathf.Sin(time * windSpeed) * swingAngle;
            float gust = (Mathf.PerlinNoise(time * .13f, 4.7f) - .5f) * gustAngle;
            transform.localRotation = Quaternion.Euler(0, restingAngle + breeze + gust, 0);
        }
    }
}

