// Verifica .

#if UNITY_EDITOR
using System;using System.Collections;using System.IO;using System.Linq;using NUnit.Framework;using UnityEngine;using UnityEngine.TestTools;using UnityEditor;
namespace HorrorGame.Tests{public sealed partial class BeforeDemolitionSchoolSceneTests{
 [Serializable]class SecretaryVisualRow{public string name,mesh,bounds,material,shader;public string[] keywords,children;}
 [Serializable]class SecretaryVisualDump{public bool loreKnown,discovered;public SecretaryVisualRow[] objects;}
 [UnityTest,Explicit,Category("SecretaryVisualDiagnostics"),Timeout(90000)]public IEnumerator DiagnoseSecretaryMaskRenderers()
 {
  var e=flow.GetComponent<SecretaryEvidence>();var w=WhistleSystem.For(game);game.player.Warp(new Vector3(-16.4f,.65f,28.2f),0);yield return Until(()=>game.narrative.HasSignal("bd.school.secretary"),"entry");e.pulseSeconds=20;w.BlowTap();yield return Aim(e.footprintLookPoint,new Vector3(-16.4f,.65f,28.2f));yield return EvidenceShot("diag-A-pulse");
  var objects=e.footprints.Concat(new[]{e.keyEcho}).Select(r=>new SecretaryVisualRow{name=r.name,mesh=AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh),bounds=r.bounds.ToString(),material=r.sharedMaterial.name,shader=r.sharedMaterial.shader.name,keywords=r.sharedMaterial.shaderKeywords,children=r.GetComponentsInChildren<Renderer>(true).Select(c=>c.name+" / "+c.enabled+" / "+c.sharedMaterial.shader.name).ToArray()}).ToArray();File.WriteAllText(SecretaryProof+"diag-runtime.json",JsonUtility.ToJson(new SecretaryVisualDump{loreKnown=w.State.loreKnown,discovered=w.State.discovered,objects=objects},true));
  e.keyEcho.gameObject.SetActive(false);yield return EvidenceShot("diag-B-no-key");e.enabled=false;yield return EvidenceShot("diag-C-no-pulse");foreach(var r in e.footprints)r.enabled=false;yield return EvidenceShot("diag-D-no-marks");
 }
}}
#endif
