// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.Linq;
using HorrorGame;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SchoolCEEP.Editor
{
    public static class SchoolItemInspectionSetup
    {
        const string Folder = "Assets/HorrorGameplay/ItemInspection";
        static TMP_FontAsset font;
        static Texture2D paper, atlas;
        static Material edge;

        public static object Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.name != "SchoolCEEP")
                throw new InvalidOperationException("Require the clean school scene outside Play. No scene is rebuilt or saved.");
            int layer = EnsureLayer();
            Directory(Folder); Directory(Folder + "/Models"); Directory(Folder + "/Materials"); Directory(Folder + "/Data");
            Directory("Assets/HorrorGameplay/Resources");
            var game = UnityEngine.Object.FindAnyObjectByType<GameSession>();
            font = game.hud.noteBody.font;
            paper = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/inspection-paper-v1.png");
            atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/inspection-story-atlas-v1.png");
            if (!paper || !atlas || !font) throw new InvalidOperationException("Missing inspection paper, atlas or existing HUD font.");
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(atlas));
            importer.maxTextureSize = 2048; importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
            var paperImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(paper));
            paperImporter.maxTextureSize = 2048; paperImporter.mipmapEnabled = true; paperImporter.wrapMode = TextureWrapMode.Clamp;
            paperImporter.textureCompression = TextureImporterCompression.CompressedHQ; paperImporter.SaveAndReimport();
            edge = Material("Paper edge", null, new Color(.42f, .35f, .23f));
            var items = new List<InspectionItemData>();
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var clue in UnityEngine.Object.FindObjectsByType<SchoolOpeningClue>(FindObjectsInactive.Include))
                {
                    string id = clue.kind == SchoolOpeningClueKind.Documents ? "school_documents"
                        : clue.kind == SchoolOpeningClueKind.Photograph ? "school_photograph" : "school_drawing";
                    var data = Item(id, clue.title, clue.body); items.Add(data);
                    if (clue.kind == SchoolOpeningClueKind.Photograph)
                    {
                        data.description = "Uma turma diante da escola. Um dos rostos foi raspado.";
                        data.frontText = "TURMA — 1984"; data.backText = "Alguns nunca foram embora.";
                        data.detailText = "Data no canto: 1984. O rosto foi apagado à mão.";
                        data.alternateBackText = "Você também nunca foi.";
                        data.model = PaperModel(data, new Rect(0, .5f, .5f, .5f), new Vector2(.5f, .46f), preview);
                    }
                    else if (clue.kind == SchoolOpeningClueKind.Drawing)
                    {
                        data.description = "Uma criança desenhou a escola e alguém na janela de cima.";
                        data.frontText = "NÃO SUBA."; data.backText = "Eu ouvi o meu nome lá em cima.";
                        data.detailText = "A figura está no último andar. A criança olha para a saída.";
                        data.alternateBackText = "Ele sabe que você voltou.";
                        data.model = PaperModel(data, new Rect(.5f, 0, .5f, .5f), new Vector2(.48f, .48f), preview);
                    }
                    else
                    {
                        data.description = "Páginas amareladas de um registro antigo da escola.";
                        data.frontText = "REGISTRO DA ESCOLA\n\n" + clue.body;
                        data.backText = "Arquivo incompleto. Não retirar os originais.";
                        data.detailText = "No rodapé: conferido em 1984.";
                        data.stressPerSecond = 0;
                        data.model = PaperModel(data, default, new Vector2(.43f, .60f), preview);
                    }
                    EditorUtility.SetDirty(data);
                }
                foreach (var clue in UnityEngine.Object.FindObjectsByType<SchoolLeftBehindClue>(FindObjectsInactive.Include))
                {
                    string id = clue.kind == SchoolLeftBehindClueKind.Report ? "old_disappearance_report"
                        : clue.kind == SchoolLeftBehindClueKind.Photograph ? "old_student_photo" : "old_key";
                    var data = Item(id, clue.title, clue.body); items.Add(data);
                    if (clue.kind == SchoolLeftBehindClueKind.Key)
                    {
                        data.description = "Ferro oxidado. Os dentes ainda parecem inteiros.";
                        data.frontText = ""; data.backText = ""; data.detailText = "Gravação na haste: ARQUIVO.";
                        data.stressPerSecond = 0; data.frontTexture = data.backTexture = null; data.model = KeyModel(data, preview);
                    }
                    else if (clue.kind == SchoolLeftBehindClueKind.Photograph)
                    {
                        data.description = "O rosto parece familiar demais.";
                        data.frontText = "1984"; data.backText = "João de Claus — aluno.";
                        data.detailText = "A inscrição foi feita com a mesma caligrafia dos registros.";
                        data.alternateBackText = "João de Claus — presente.";
                        data.model = PaperModel(data, new Rect(.5f, .5f, .5f, .5f), new Vector2(.43f, .48f), preview);
                    }
                    else
                    {
                        data.description = "Um relatório esquecido no arquivo antigo.";
                        data.frontText = "RELATÓRIO INTERNO\n\n" + clue.body;
                        data.backText = "NOME NO REGISTRO:\nJoão de Claus\n\nNão divulgar.";
                        data.detailText = "A assinatura foi arrancada. A data continua legível: 1984.";
                        data.alternateBackText = "ELE LEMBROU DE VOCÊ.";
                        data.model = PaperModel(data, default, new Vector2(.43f, .60f), preview);
                    }
                    EditorUtility.SetDirty(data);
                }
                foreach (var clue in UnityEngine.Object.FindObjectsByType<SchoolMemoriesClue>(FindObjectsInactive.Include))
                {
                    string id = clue.kind == SchoolMemoriesClueKind.RecordingOne ? "memory_recording_one"
                        : clue.kind == SchoolMemoriesClueKind.RecordingTwo ? "memory_recording_two" : "memories_group_photo";
                    var data = Item(id, clue.title, clue.body); items.Add(data);
                    if (clue.kind == SchoolMemoriesClueKind.Photograph)
                    {
                        data.description = "A promessa escrita sobre uma lembrança que João não reconhece.";
                        data.frontText = "PROMETE QUE VOLTA?"; data.backText = "João de Claus — aluno.";
                        data.detailText = "João está na mesma turma da primeira fotografia.";
                        data.model = PaperModel(data, new Rect(0, 0, .5f, .5f), new Vector2(.50f, .46f), preview);
                    }
                    else
                    {
                        data.description = "Um gravador de fita. Há uma gravação preservada.";
                        data.canPlay = true; data.frontText = ""; data.backText = "";
                        data.detailText = clue.kind == SchoolMemoriesClueKind.RecordingOne ? "Etiqueta: FITA 1 — 1984." : "Etiqueta: FITA 2 — 1984.";
                        data.model = PropModel(data, clue.visual, preview);
                        data.recording = TapeSound(); data.recordingText = clue.body;
                    }
                    EditorUtility.SetDirty(data);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            if (items.Count != 9 || items.Select(x => x.itemId).Distinct().Count() != 9)
                throw new InvalidOperationException("Expected the nine existing story items, each with its original inventory ID.");
            var catalog = AssetDatabase.LoadAssetAtPath<SchoolInspectionCatalog>("Assets/HorrorGameplay/Resources/SchoolInspectionItems.asset");
            if (!catalog) { catalog = ScriptableObject.CreateInstance<SchoolInspectionCatalog>(); AssetDatabase.CreateAsset(catalog, "Assets/HorrorGameplay/Resources/SchoolInspectionItems.asset"); }
            catalog.items = items.ToArray(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            return new { layer, items = items.Count, models = items.Select(x => AssetDatabase.GetAssetPath(x.model)).ToArray(), sceneDirty = scene.isDirty,
                note = "Only new inspection assets and an unused layer were created. The school, menu and prologue scenes are untouched." };
        }
        static void Directory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/'); Directory(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
        static int EnsureLayer()
        {
            int existing = LayerMask.NameToLayer("Inspect"); if (existing >= 0) return existing;
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = settings.FindProperty("layers");
            for (int i = 31; i >= 8; i--)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                { layers.GetArrayElementAtIndex(i).stringValue = "Inspect"; settings.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets(); return i; }
            throw new InvalidOperationException("No unused layer is available for Inspect.");
        }
        static InspectionItemData Item(string id, string title, string body)
        {
            string path = Folder + "/Data/" + id + ".asset";
            var item = AssetDatabase.LoadAssetAtPath<InspectionItemData>(path);
            if (!item) { item = ScriptableObject.CreateInstance<InspectionItemData>(); AssetDatabase.CreateAsset(item, path); }
            item.itemId = id; item.title = title; item.frontTexture = atlas; item.backTexture = paper;
            item.pickupSound = Sound(id == "old_key" ? "Key pickup" : "Paper pickup");
            item.whisper = Sound("Whisper"); item.recordInNotes = true;
            return item;
        }
        static SoundSet Sound(string name)
        {
            return AssetDatabase.FindAssets("t:SoundSet").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SoundSet>).FirstOrDefault(x => x.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        static SoundSet TapeSound()
        {
            string path=Folder+"/Data/Tape playback.asset";
            var set=AssetDatabase.LoadAssetAtPath<SoundSet>(path);
            if(set)return set;
            string wav=Folder+"/Data/tape-playback.wav";
            const int rate=16000, count=rate*10;
            using(var writer=new System.IO.BinaryWriter(System.IO.File.Create(wav)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);
                writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
                var random=new System.Random(1984);float smooth=0;
                for(int i=0;i<count;i++)
                {
                    float time=(float)i/rate; smooth=smooth*.82f+((float)random.NextDouble()*2-1)*.18f;
                    float envelope=Mathf.Min(1,time*12)*Mathf.Clamp01((10-time)*4);
                    float motor=Mathf.Sin(time*2*Mathf.PI*53)*.012f+Mathf.Sin(time*2*Mathf.PI*107)*.005f;
                    float click=(i<120||i>count-120)?Mathf.Sin(i*.46f)*.20f:0;
                    writer.Write((short)(Mathf.Clamp((smooth*.16f+motor+click)*envelope,-1,1)*short.MaxValue));
                }
            }
            AssetDatabase.ImportAsset(wav,ImportAssetOptions.ForceSynchronousImport);
            var importer=(AudioImporter)AssetImporter.GetAtPath(wav);var settings=importer.defaultSampleSettings;
            settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.8f;
            importer.defaultSampleSettings=settings;importer.forceToMono=true;importer.SaveAndReimport();
            set=ScriptableObject.CreateInstance<SoundSet>();set.clips=new[]{AssetDatabase.LoadAssetAtPath<AudioClip>(wav)};
            set.pitch=Vector2.one;set.volume=Vector2.one;AssetDatabase.CreateAsset(set,path);return set;
        }
        static Material Material(string name, Texture texture, Color color)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", .08f); material.SetFloat("_Metallic", 0);
            material.enableInstancing = true; EditorUtility.SetDirty(material); return material;
        }
        static GameObject PaperModel(InspectionItemData data, Rect area, Vector2 size, Scene preview)
        {
            data.frontTexture = area.width > 0 ? atlas : paper;
            var root = new GameObject(data.itemId); SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                var mesh = new Mesh { name = data.itemId + " — two sided worn paper" };
                float x = size.x * .5f, y = size.y * .5f, thickness = .002f;
                var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int[]>();
                Vector3[] front = { new Vector3(-x,-y,0), new Vector3(x,-y,0),new Vector3(x,y,-.008f),new Vector3(-x,y,0) };
                vertices.AddRange(front); uv.AddRange(new[] {Vector2.zero,Vector2.right,Vector2.one,Vector2.up}); triangles.Add(new[]{0,2,1,0,3,2});
                vertices.AddRange(front.Select(v=>v+Vector3.forward*thickness));
                uv.AddRange(new[] {Vector2.right,Vector2.zero,Vector2.up,Vector2.one}); triangles.Add(new[]{4,5,6,4,6,7});
                var sides = new List<int>();
                for(int i=0;i<4;i++) { int next=(i+1)%4; int n=vertices.Count;vertices.Add(front[i]);vertices.Add(front[next]);vertices.Add(front[next]+Vector3.forward*thickness);vertices.Add(front[i]+Vector3.forward*thickness);uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});sides.AddRange(new[]{n,n+1,n+2,n,n+2,n+3}); }
                mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.subMeshCount=3;
                for(int i=0;i<3;i++)mesh.SetTriangles(i<2?triangles[i]:sides.ToArray(),i);mesh.RecalculateNormals();mesh.RecalculateBounds();
                string meshPath=Folder+"/Models/"+data.itemId+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,meshPath);
                var face=Material(data.itemId+" front",area.width>0?atlas:paper,Color.white);
                if(area.width>0){float inset=2f/atlas.width;face.SetTextureScale("_BaseMap",new Vector2(area.width-inset*2,area.height-inset*2));face.SetTextureOffset("_BaseMap",area.position+Vector2.one*inset);}else{face.SetTextureScale("_BaseMap",Vector2.one);face.SetTextureOffset("_BaseMap",Vector2.zero);}
                var back=Material(data.itemId+" back",paper,new Color(.94f,.89f,.76f));
                root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterials=new[]{face,back,edge};
                if(!string.IsNullOrWhiteSpace(data.frontText))Text(root.transform,"Front inscription",data.frontText,new Vector3(0,area.width>0?-y*.78f:0,-.011f),Quaternion.identity,new Vector2(size.x*.9f,area.width>0?.07f:size.y*.85f),area.width>0?.15f:.095f);
                Text(root.transform,"Back inscription",data.backText,new Vector3(0,0,.005f),Quaternion.Euler(0,180,0),new Vector2(size.x*.85f,size.y*.76f),.18f);
                Text(root.transform,"Hidden detail",data.detailText,new Vector3(0,y*.78f,.006f),Quaternion.Euler(0,180,0),new Vector2(size.x*.85f,.045f),.047f);
                return PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Models/"+data.itemId+".prefab");
            } finally {UnityEngine.Object.DestroyImmediate(root);}
        }
        static GameObject PropModel(InspectionItemData data, GameObject visual, Scene preview)
        {
            if(!visual)throw new InvalidOperationException("Missing existing prop visual: "+data.itemId);
            var root=new GameObject(data.itemId);SceneManager.MoveGameObjectToScene(root,preview);
            try
            {
                var copy=UnityEngine.Object.Instantiate(visual,root.transform);copy.name="Existing story prop";copy.SetActive(true);
                foreach(var c in copy.GetComponentsInChildren<MonoBehaviour>(true))if(!(c is TMP_Text))UnityEngine.Object.DestroyImmediate(c);
                foreach(var c in copy.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
                copy.transform.localPosition=Vector3.zero;copy.transform.localRotation=Quaternion.Euler(-90,0,0);copy.transform.localScale=Vector3.one;
                var renderers=copy.GetComponentsInChildren<Renderer>(true);var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
                float scale=.46f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                copy.transform.localScale*=scale;copy.transform.localPosition-=bounds.center*scale;
                foreach(var renderer in renderers.Where(x=>x is MeshRenderer && !x.GetComponent<TMP_Text>()))
                {
                    renderer.sharedMaterials=renderer.sharedMaterials.Select((m,i)=>{
                        var color=m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):Color.gray;
                        float brightest=Mathf.Max(color.r,color.g,color.b);if(brightest<.18f)color*=.18f/Mathf.Max(.01f,brightest);
                        var material=Material(data.itemId+" prop "+m.name,null,color);
                        material.SetFloat("_Metallic",data.itemId=="old_key"?.65f:.22f);material.SetFloat("_Smoothness",.27f);return material;
                    }).ToArray();
                }
                return PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Models/"+data.itemId+".prefab");
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        static GameObject KeyModel(InspectionItemData data,Scene preview)
        {
            var root=new GameObject(data.itemId);SceneManager.MoveGameObjectToScene(root,preview);
            try
            {
                var brass=Material("Old key brass",null,new Color(.46f,.32f,.17f));brass.SetFloat("_Metallic",.45f);brass.SetFloat("_Smoothness",.3f);
                var mesh=new Mesh{name="Old key — continuous ring"};var vertices=new List<Vector3>();var indices=new List<int>();
                const int segments=32;const float outside=.075f,inside=.050f,halfDepth=.009f;
                for(int i=0;i<segments;i++)
                {
                    float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                    Vector2 innerA=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*inside,innerB=new Vector2(Mathf.Cos(b),Mathf.Sin(b))*inside;
                    Vector2 outerA=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*outside,outerB=new Vector2(Mathf.Cos(b),Mathf.Sin(b))*outside;
                    Vector3 ia=new Vector3(innerA.x-.16f,innerA.y,-halfDepth),ib=new Vector3(innerB.x-.16f,innerB.y,-halfDepth),oa=new Vector3(outerA.x-.16f,outerA.y,-halfDepth),ob=new Vector3(outerB.x-.16f,outerB.y,-halfDepth);
                    foreach(var quad in new[]{new[]{ia,oa,ob,ib},new[]{ia+Vector3.forward*halfDepth*2,ib+Vector3.forward*halfDepth*2,ob+Vector3.forward*halfDepth*2,oa+Vector3.forward*halfDepth*2},new[]{oa,oa+Vector3.forward*halfDepth*2,ob+Vector3.forward*halfDepth*2,ob},new[]{ia,ib,ib+Vector3.forward*halfDepth*2,ia+Vector3.forward*halfDepth*2}})
                    {int n=vertices.Count;vertices.AddRange(quad);indices.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});}
                }
                mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                string path=Folder+"/Models/old_key_ring.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
                var ring=new GameObject("Continuous worn ring");ring.transform.SetParent(root.transform,false);ring.AddComponent<MeshFilter>().sharedMesh=mesh;ring.AddComponent<MeshRenderer>().sharedMaterial=brass;
                var pieces=new[]{new{p=new Vector3(.06f,0,0),s=new Vector3(.30f,.028f,.018f)},new{p=new Vector3(.14f,-.027f,0),s=new Vector3(.032f,.052f,.018f)},new{p=new Vector3(.20f,-.024f,0),s=new Vector3(.027f,.045f,.018f)}};
                foreach(var piece in pieces){var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name="Worn key blade";part.transform.SetParent(root.transform,false);part.transform.localPosition=piece.p;part.transform.localScale=piece.s;UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());part.GetComponent<MeshRenderer>().sharedMaterial=brass;}
                Text(root.transform,"Archive engraving","ARQUIVO",new Vector3(.01f,0,-.010f),Quaternion.identity,new Vector2(.19f,.025f),.045f);
                return PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Models/old_key.prefab");
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        static void Text(Transform root,string name,string value,Vector3 position,Quaternion rotation,Vector2 size,float pointSize)
        {
            var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.localPosition=position;go.transform.localRotation=rotation;
            var text=go.AddComponent<TextMeshPro>();text.font=font;text.fontSize=pointSize;text.color=new Color(.075f,.067f,.05f);
            text.text=value;text.rectTransform.sizeDelta=size;text.alignment=TextAlignmentOptions.Center;text.textWrappingMode=TextWrappingModes.Normal;
            text.overflowMode=TextOverflowModes.Overflow;text.raycastTarget=false;
        }
    }
}
