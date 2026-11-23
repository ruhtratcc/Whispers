// Controla o ambiente e eventos de fundo do menu.

using UnityEngine;
namespace SchoolMenu {
public sealed class MenuAtmosphere : MonoBehaviour {
 [SerializeField] public Camera menuCamera;
 [SerializeField] public Light fluorescent;
 [SerializeField] public Renderer tube;
 [SerializeField] float driftPosition = .018f, driftAngle = .13f, driftSpeed = .19f;
 [SerializeField] Vector2 flickerInterval = new Vector2(6,14);
 [SerializeField] float baseIntensity = 6f;
 public Vector3 PresentationOffset { get; set; }
 Vector3 home; Quaternion rotation; float nextFlicker, flickerUntil;
 void Awake() { if(menuCamera){home=menuCamera.transform.position;rotation=menuCamera.transform.rotation;} nextFlicker=Time.unscaledTime+3f; }
 void Update() {
  float t=Time.unscaledTime;
  if(menuCamera){menuCamera.transform.position=home+PresentationOffset+new Vector3(Mathf.Sin(t*driftSpeed)*driftPosition,Mathf.Sin(t*driftSpeed*.71f)*driftPosition,0);menuCamera.transform.rotation=rotation*Quaternion.Euler(Mathf.Sin(t*.13f)*driftAngle,Mathf.Sin(t*.17f)*driftAngle,0);}
  if(t>nextFlicker){flickerUntil=t+.22f;nextFlicker=t+Random.Range(flickerInterval.x,flickerInterval.y);}
  float power=t<flickerUntil?(Mathf.Sin(t*97f)>0?.08f:1f):1f;
  if(t<2.4f)power*=Mathf.Clamp01((t-.35f)/1.5f)*(Mathf.Sin(t*29f)>.7f?.3f:1f);
  if(fluorescent)fluorescent.intensity=baseIntensity*power;
  if(tube)tube.enabled=power>.15f;
 }
}
}
