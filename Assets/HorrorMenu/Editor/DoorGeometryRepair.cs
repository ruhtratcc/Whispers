// Ferramenta de preparação da cena e da apresentação do menu.

using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace SchoolMenu.Editor
{
    public static class DoorGeometryRepair
    {
        static Transform Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name=name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition=position;
            go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.transform;
        }

        public static string Apply()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(EditorApplication.isPlaying||scene.path!="Assets/HorrorMenu/Scenes/MainMenu.unity")
                throw new System.InvalidOperationException("Open MainMenu outside Play Mode.");
            var corridor=GameObject.Find("School corridor").transform;
            if(corridor.Find("Room visible through left doorway"))return Finish();
            var room=new GameObject("Room visible through left doorway").transform;
            room.SetParent(corridor,false);
            // The original long wall, trim and solid frame covered the entire supposed opening.
            const float near=9.36f, far=10.64f;
            foreach(var name in new[]{"Upper wall","Lower wall","Dado rail","Skirting"})
            {
                var wall=corridor.Cast<Transform>().First(t=>t.name==name&&t.localPosition.x<0);
                var position=wall.localPosition;
                var size=wall.localScale;
                float start=position.z-size.z*.5f, end=position.z+size.z*.5f;
                var material=wall.GetComponent<Renderer>().sharedMaterial;
                Box("Left wall continuation "+name,room,
                    new Vector3(position.x,position.y,(far+end)*.5f),
                    new Vector3(size.x,size.y,end-far),material);
                wall.localPosition=new Vector3(position.x,position.y,(start+near)*.5f);
                wall.localScale=new Vector3(size.x,size.y,near-start);
                if(name=="Upper wall")
                    Box("Wall above the open classroom",room,new Vector3(position.x,2.975f,10),
                        new Vector3(size.x,1.15f,far-near),material);
            }
            var frame=corridor.Cast<Transform>().Where(t=>t.name=="Classroom frame")
                .OrderBy(t=>Mathf.Abs(t.position.z-10)).First();
            var frameMaterial=frame.GetComponent<Renderer>().sharedMaterial;
            frame.localPosition=new Vector3(-2.69f,1.22f,near);
            frame.localScale=new Vector3(.17f,2.5f,.1f);
            Box("Left doorway far jamb",room,new Vector3(-2.69f,1.22f,far),new Vector3(.17f,2.5f,.1f),frameMaterial);
            Box("Left doorway lintel",room,new Vector3(-2.69f,2.45f,10),new Vector3(.17f,.1f,1.38f),frameMaterial);
            var hinge=corridor.Find("Left classroom hinge");
            var door=hinge.Find("Classroom door");
            hinge.localPosition=new Vector3(-2.67f,0,10.565f);
            hinge.localRotation=Quaternion.Euler(0,-28,0);
            door.localPosition=new Vector3(0,1.17f,-.565f);
            door.localRotation=Quaternion.identity;
            door.localScale=new Vector3(.08f,2.34f,1.13f);
            var wind=new SerializedObject(hinge.GetComponent<WindDoor>());
            wind.FindProperty("restingAngle").floatValue=-28;
            wind.FindProperty("swingAngle").floatValue=4;
            wind.ApplyModifiedPropertiesWithoutUndo();
            var glass=door.Find("Door glass");
            glass.localPosition=new Vector3(.56f,.205f,0);
            glass.localScale=new Vector3(.10f,.277f,.557f);
            var handle=door.Find("Door handle");
            handle.localPosition=new Vector3(.95f,-.073f,-.37f);
            handle.localScale=new Vector3(.9f,.015f,.15f);

            var plaster=AssetDatabase.LoadAssetAtPath<Material>("Assets/HorrorMenu/Art/FadedPaint.mat");
            var dark=AssetDatabase.LoadAssetAtPath<Material>("Assets/HorrorMenu/Art/DarkMetal.mat");
            var recess=corridor.Find("Open classroom recess");
            recess.SetParent(room,false);
            recess.localPosition=new Vector3(-3.96f,1.20f,12.85f);
            recess.localRotation=Quaternion.identity;
            recess.localScale=new Vector3(2.45f,2.40f,.12f);
            recess.GetComponent<Renderer>().sharedMaterial=plaster;
            Box("Classroom inner back wall",room,new Vector3(-5.12f,1.2f,10.6f),new Vector3(.12f,2.4f,4.5f),plaster);
            Box("Classroom inner near wall",room,new Vector3(-3.96f,1.2f,8.35f),new Vector3(2.45f,2.4f,.12f),plaster);
            Box("Classroom inner ceiling",room,new Vector3(-3.96f,2.45f,10.6f),new Vector3(2.45f,.1f,4.5f),dark);
            Box("Classroom inner floor",room,new Vector3(-3.96f,-.045f,10.6f),new Vector3(2.45f,.05f,4.5f),dark);

            // Place the light inside the room instead of against the door handle.
            var inside=corridor.Find("Left classroom interior light").GetComponent<Light>();
            inside.transform.localPosition=new Vector3(-3.2f,1.35f,11.0f);
            inside.transform.localRotation=Quaternion.LookRotation(new Vector3(1,-.3f,-.35f));
            inside.intensity=1.7f;
            inside.spotAngle=85;
            inside.innerSpotAngle=45;
            var spill=corridor.Find("Classroom light spill").GetComponent<Light>();
            spill.transform.localPosition=new Vector3(-3.1f,1.65f,11.2f);
            spill.intensity=1.3f;
            spill.range=3.8f;
            spill.shadows=LightShadows.Soft;

            string glassPath="Assets/HorrorMenu/Art/DustyDoorGlass.mat";
            var glassMaterial=AssetDatabase.LoadAssetAtPath<Material>(glassPath);
            if(!glassMaterial)
            {
                glassMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                glassMaterial.name="Dusty door glass";
                glassMaterial.SetColor("_BaseColor",new Color(.027f,.034f,.031f));
                glassMaterial.SetFloat("_Smoothness",.08f);
                glassMaterial.SetFloat("_Metallic",0);
                AssetDatabase.CreateAsset(glassMaterial,glassPath);
            }
            glass.GetComponent<Renderer>().sharedMaterial=glassMaterial;
            EditorUtility.SetDirty(hinge.GetComponent<WindDoor>());
            EditorUtility.SetDirty(inside);
            EditorUtility.SetDirty(spill);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return Finish();
        }

        public static string Finish()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play Mode first.");
            var corridor=GameObject.Find("School corridor").transform;
            var room=corridor.Find("Room visible through left doorway");
            foreach(var name in new[]{"Upper wall","Lower wall","Dado rail","Skirting"})
            {
                var wall=corridor.Cast<Transform>().First(t=>t.name==name&&t.position.x<0);
                var p=wall.localPosition;var size=wall.localScale;float start=p.z-size.z*.5f;
                wall.localPosition=new Vector3(p.x,p.y,(start+9.04f)*.5f);
                wall.localScale=new Vector3(size.x,size.y,9.04f-start);
            }
            var frame=corridor.Cast<Transform>().First(t=>t.name=="Classroom frame"&&t.position.x<0&&Mathf.Abs(t.position.z-9.2f)<.4f);
            frame.localPosition=new Vector3(-2.83f,1.22f,9.04f);
            frame.localScale=new Vector3(.08f,2.5f,.08f);
            room.Find("Left doorway far jamb").localScale=new Vector3(.08f,2.5f,.08f);
            room.Find("Left doorway far jamb").localPosition=new Vector3(-2.83f,1.22f,10.64f);
            corridor.Find("Left classroom hinge").localPosition=new Vector3(-2.79f,0,10.565f);
            foreach(var name in new[]{"Upper wall","Lower wall"})
            foreach(var wall in new[]{corridor.Cast<Transform>().First(t=>t.name==name&&t.position.x<0),room.Find("Left wall continuation "+name)})
            {
                var p=wall.localPosition;var size=wall.localScale;
                wall.localPosition=new Vector3(-2.83f,p.y,p.z);
                wall.localScale=new Vector3(.08f,size.y,size.z);
            }
            var lintel=room.Find("Left doorway lintel");
            lintel.localPosition=new Vector3(-2.69f,2.45f,9.84f);
            lintel.localScale=new Vector3(.1f,.1f,1.68f);
            var top=room.Find("Wall above the open classroom");
            top.localPosition=new Vector3(-2.85f,2.975f,9.84f);
            top.localScale=new Vector3(.18f,1.15f,1.6f);
            var door=corridor.Find("Left classroom hinge").Find("Classroom door");
            door.localPosition=new Vector3(0,1.17f,-.75f);
            door.localScale=new Vector3(.08f,2.34f,1.5f);
            door.Find("Door glass").localScale=new Vector3(.1f,.277f,.42f);
            door.Find("Door handle").localPosition=new Vector3(.95f,-.073f,-.4f);
            string path="Assets/HorrorMenu/Art/DimClassroomInterior.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat)
            {
                mat=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/HorrorMenu/Art/FadedPaint.mat"));
                mat.name="Dim classroom interior";AssetDatabase.CreateAsset(mat,path);
            }
            mat.EnableKeyword("_EMISSION");
            string texturePath="Assets/HorrorMenu/Art/ClassroomLightFalloff.asset";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(!texture)
            {
                texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
                texture.name="Classroom light falloff";texture.wrapMode=TextureWrapMode.Clamp;
                var colors=new Color[64*64];
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)
                {
                    float height=(y+.5f)/64f;
                    float falloff=.25f+.75f*Mathf.Sin(height*Mathf.PI);
                    float wear=.38f+.48f*Mathf.PerlinNoise(x*.13f+2,y*.11f+4);
                    float value=falloff*wear;
                    colors[y*64+x]=new Color(value,value,value,1);
                }
                texture.SetPixels(colors);texture.Apply();AssetDatabase.CreateAsset(texture,texturePath);
            }
            mat.SetTexture("_EmissionMap",texture);
            mat.SetTextureScale("_EmissionMap",Vector2.one);
            mat.SetColor("_EmissionColor",new Color(.65f,.37f,.15f));
            mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
            UnityEditor.MaterialEditor.FixupEmissiveFlag(mat);
            mat.EnableKeyword("_EMISSION");
            room.Find("Open classroom recess").GetComponent<Renderer>().sharedMaterial=mat;
            room.Find("Classroom inner back wall").GetComponent<Renderer>().sharedMaterial=mat;
            var spot=corridor.Find("Left classroom interior light").GetComponent<Light>();
            spot.transform.localPosition=new Vector3(-3,1.1f,10);
            spot.transform.localRotation=Quaternion.LookRotation(new Vector3(1,-.8f,-.2f));
            spot.intensity=1.2f;spot.range=3.5f;spot.spotAngle=100;spot.innerSpotAngle=65;
            var point=corridor.Find("Classroom light spill").GetComponent<Light>();point.intensity=.75f;
            EditorUtility.SetDirty(mat);EditorUtility.SetDirty(spot);EditorUtility.SetDirty(point);
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            return "Visible doorway, aligned fittings and dim interior saved.";
        }
    }
}


