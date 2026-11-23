// Ferramenta de preparação ou verificação usada no Editor.

using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorGame.Editor
{
    public static class OfficeSceneBuilder
    {
        public const string Root = "Assets/ChapterOneOffice/";
        public const string ScenePath = Root + "Scenes/Chapter1Office.unity";
        static Transform room;
        static int world, interact;
        static Material plaster, panel, timber, darkWood, steel, brass, leather, paper, cream, black, rug, glass, glow, green, cork;
        static TMP_FontAsset font;
        static int serial;
        static readonly System.Random random = new System.Random(9127);

        static Transform Group(string name, Vector3 pos, Transform parent = null, float yaw = 0)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent ? parent : room, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0, yaw, 0);
            return t;
        }

        static Material Mat(string name, Color tint, int pattern = 0, float smooth = .15f, float metallic = 0)
        {
            var path = Root + "Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            var texture = new Texture2D(256, 256, TextureFormat.RGB24, true) { name = name + " surface", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            var data = new Color[256 * 256];
            var noise = new System.Random(name.GetHashCode());
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                float p = Mathf.PerlinNoise(x * .025f, y * .03f);
                float n = .83f + p * .22f + (float)noise.NextDouble() * .045f;
                if (pattern == 1) n *= .78f + .23f * Mathf.PerlinNoise(x * .065f, y * .005f) + .035f * Mathf.Sin(x * .39f + p * 6);
                if (pattern == 2) n *= ((x / 8 + y / 8) % 2 == 0 ? .95f : .83f);
                if (pattern == 3) n *= (x % 64 < 2 || y % 64 < 2) ? .68f : 1;
                if (pattern == 4 && (y % 16 < 2)) n *= .74f;
                data[y * 256 + x] = tint * n;
            }
            texture.SetPixels(data); texture.Apply();
            AssetDatabase.CreateAsset(texture, Root + "Textures/" + name + ".asset");
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = name; m.SetTexture("_BaseMap", texture); m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metallic);
            AssetDatabase.CreateAsset(m, path); return m;
        }

        static GameObject Box(string name, Vector3 pos, Vector3 size, Material material, Transform parent = null, bool collision = false, Vector3 rotation = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.layer = world; go.transform.SetParent(parent ? parent : room, false);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(rotation); go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static Mesh ChamferedBox(Vector3 size)
        {
            string key = size.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "_" + size.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "_" + size.z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
            string path = Root + "Models/Bevel_" + key + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing) return existing;
            float radius = Mathf.Min(.022f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * .17f);
            Vector3 b = new Vector3(radius / size.x, radius / size.y, radius / size.z);
            var vertices = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();
            Vector3 A(float x, float y, float z) => new Vector3(x*.5f,y*(.5f-b.y),z*(.5f-b.z));
            Vector3 B(float x, float y, float z) => new Vector3(x*(.5f-b.x),y*.5f,z*(.5f-b.z));
            Vector3 C(float x, float y, float z) => new Vector3(x*(.5f-b.x),y*(.5f-b.y),z*.5f);
            void Face(params Vector3[] points)
            {
                Vector3 center=Vector3.zero;foreach(var p in points)center+=p;center/=points.Length;
                if(Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),center)<0)Array.Reverse(points);
                int start=vertices.Count;foreach(var p in points){vertices.Add(p);uv.Add(new Vector2(p.x+p.z+.5f,p.y+.5f));}
                for(int i=1;i<points.Length-1;i++){triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);}
            }
            foreach(float s in new[]{-1f,1f})
            {
                Face(A(s,-1,-1),A(s,1,-1),A(s,1,1),A(s,-1,1));
                Face(B(-1,s,-1),B(1,s,-1),B(1,s,1),B(-1,s,1));
                Face(C(-1,-1,s),C(1,-1,s),C(1,1,s),C(-1,1,s));
            }
            foreach(float x in new[]{-1f,1f})foreach(float y in new[]{-1f,1f})Face(A(x,y,-1),B(x,y,-1),B(x,y,1),A(x,y,1));
            foreach(float x in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})Face(A(x,-1,z),C(x,-1,z),C(x,1,z),A(x,1,z));
            foreach(float y in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})Face(B(-1,y,z),C(-1,y,z),C(1,y,z),B(1,y,z));
            foreach(float x in new[]{-1f,1f})foreach(float y in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})Face(A(x,y,z),B(x,y,z),C(x,y,z));
            var mesh=new Mesh{name="Chamfered furniture " + key};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }

        public static string DetailScene()
        {
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=ScenePath||EditorApplication.isPlaying)throw new InvalidOperationException("Open stopped office scene first.");
            room=GameObject.Find("Escritório de João de Claus — Capítulo 1").transform;
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/HorrorMenu/TMP/Resources/Fonts & Materials/LiberationSans SDF.asset");
            brass=Mat("Tarnished brass",new Color(.55f,.40f,.17f));paper=Mat("Aged paper",new Color(.78f,.73f,.58f));
            steel=Mat("Aged gunmetal",new Color(.20f,.22f,.21f));panel=Mat("Muted olive paint",new Color(.23f,.27f,.22f));
            darkWood=Mat("Dark walnut",new Color(.13f,.079f,.045f));black=Mat("Black bakelite",new Color(.035f,.04f,.035f));
            int count=0;
            var names=new[]{"Tampo de madeira","Assento acolchoado","Encosto","Monitor CRT","Base do telefone","Frente entalhada","Gabinete","Plaqueta","Cúpula verde"};
            foreach(var f in UnityEngine.Object.FindObjectsByType<MeshFilter>())if(names.Contains(f.name)){f.sharedMesh=ChamferedBox(f.transform.localScale);EditorUtility.SetDirty(f);count++;}
            var desk=GameObject.Find("Mesa principal — João de Claus").transform;
            if(!desk.Find("Fotografia emoldurada"))
            {
                var frame=Group("Fotografia emoldurada",new Vector3(.39f,.985f,.35f),desk,12);
                Box("Moldura pequena",Vector3.zero,new Vector3(.21f,.26f,.025f),brass,frame);
                Box("Foto antiga",new Vector3(0,0,-.014f),new Vector3(.18f,.23f,.003f),paper,frame);
                Box("Rua na fotografia",new Vector3(0,-.04f,-.017f),new Vector3(.16f,.035f,.001f),steel,frame);
                for(int i=0;i<5;i++)Box("Fachada fotografada",new Vector3(-.065f+i*.033f,.013f,-.018f),new Vector3(.025f,.09f+(i%2)*.025f,.001f),i%2==0?panel:darkWood,frame);
                Text("Identificação da foto","ARQUIVO PESSOAL",new Vector3(0,-.103f,-.02f),Vector3.zero,new Vector2(.17f,.025f),1.2f,frame);
                Rod("Suporte do porta-retrato",new Vector3(0,-.13f,.02f),new Vector3(0,-.13f,.12f),.009f,black,frame);
                var cable=Group("Fiação do computador",new Vector3(-1.1f,.1f,1.32f));
                for(int i=0;i<14;i++)Rod("Cabo",new Vector3(-.2f+i*.07f,.012f,Mathf.Sin(i*.4f)*.08f),new Vector3(-.2f+(i+1)*.07f,.012f,Mathf.Sin((i+1)*.4f)*.08f),.004f,black,cable);
                var wallMap=Group("Mapa de localização dos casos",new Vector3(.24f,1.88f,3.54f));
                Box("Moldura do mapa",Vector3.zero,new Vector3(1.16f,.77f,.025f),darkWood,wallMap);
                Box("Mapa envelhecido",new Vector3(0,0,-.018f),new Vector3(1.08f,.69f,.004f),paper,wallMap);
                for(int i=0;i<7;i++)Rod("Rua desenhada",new Vector3(-.5f,-.26f+i*.082f,-.023f),new Vector3(.5f,-.21f+i*.069f,-.023f),.0016f,steel,wallMap);
                for(int i=0;i<8;i++)Rod("Travessa",new Vector3(-.46f+i*.13f,-.31f,-.023f),new Vector3(-.43f+i*.12f,.31f,-.023f),.0012f,steel,wallMap);
                Text("Legenda do mapa","LOCALIZAÇÃO DOS CASOS",new Vector3(0,.25f,-.026f),Vector3.zero,new Vector2(.8f,.10f),3,wallMap);
                foreach(var p in new[]{new Vector3(-.2f,.03f,-.029f),new Vector3(.28f,-.17f,-.029f),new Vector3(.13f,.14f,-.029f)})Round("Marcação no mapa",p,new Vector3(.015f,.015f,.009f),brass,wallMap,true);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            return count + " pieces now have chamfered geometry; photograph, map and cables added.";
        }

        static GameObject Round(string name, Vector3 pos, Vector3 size, Material material, Transform parent, bool sphere = false)
        {
            var go = GameObject.CreatePrimitive(sphere ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
            go.name = name; go.layer = world; go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }

        static void Rod(string name, Vector3 a, Vector3 b, float radius, Material material, Transform parent)
        {
            var go = Round(name, (a + b) * .5f, new Vector3(radius * 2, Vector3.Distance(a, b) * .5f, radius * 2), material, parent);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        }

        static void Collision(Transform root, Vector3 center, Vector3 size)
        {
            var c = root.gameObject.AddComponent<BoxCollider>(); c.center = center; c.size = size; root.gameObject.layer = world;
        }

        static TMP_Text Text(string name, string value, Vector3 pos, Vector3 euler, Vector2 size, float points, Transform parent, Color? color = null)
        {
            var t = new GameObject(name, typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            t.transform.SetParent(parent, false); t.transform.localPosition = pos; t.transform.localRotation = Quaternion.Euler(euler);
            t.rectTransform.sizeDelta = size; t.font = font; t.fontSize = points * .1f; t.color = color ?? new Color(.18f, .17f, .13f);
            t.alignment = TextAlignmentOptions.Center; t.text = value; t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        static Light Light(string name, Vector3 pos, Color color, float intensity, float range, Transform parent = null, LightType type = LightType.Point, Vector3 rotation = default)
        {
            var t = Group(name, pos, parent); t.localRotation = Quaternion.Euler(rotation);
            var l = t.gameObject.AddComponent<Light>(); l.type = type; l.color = color; l.intensity = intensity; l.range = range;
            l.shadows = LightShadows.Soft; l.shadowBias = .035f; l.shadowNormalBias = .12f;
            if (type == LightType.Spot) { l.spotAngle = 100; l.innerSpotAngle = 62; }
            return l;
        }

        static void Page(Transform parent, Vector3 pos, float yaw, string heading, bool readable = false, string body = "")
        {
            var page = Group("Documento — " + heading, pos, parent, yaw);
            Box("Papel amarelado", Vector3.zero, new Vector3(.21f, .002f, .29f), paper, page);
            Text("Título impresso", heading, new Vector3(0, .0025f, .08f), new Vector3(90, 0, 0), new Vector2(.19f, .08f), 2.1f, page);
            for (int i = 0; i < 9; i++) Box("Linha datilografada", new Vector3(i % 3 == 0 ? -.019f : 0, .0028f, .034f - i * .016f), new Vector3(i % 3 == 0 ? .13f : .17f, .0007f, .0013f), steel, page);
            if (readable)
            {
                var c = page.gameObject.AddComponent<BoxCollider>(); c.size = new Vector3(.23f, .025f, .31f);
                page.gameObject.layer = interact;
                var note = page.gameObject.AddComponent<CollectibleItem>(); note.kind = CollectibleKind.Note;
                note.itemId = "office_note_" + serial++; note.persistentId = Guid.NewGuid().ToString("N"); note.title = heading; note.noteText = body;
                note.pickupSound = AssetDatabase.LoadAssetAtPath<SoundSet>("Assets/HorrorGameplay/Audio/Pickup.asset");
            }
        }

        static void Books(Transform shelf, float level, int count, float start)
        {
            for (int i = 0; i < count; i++)
            {
                float width = .033f + (float)random.NextDouble() * .03f;
                float height = .18f + (float)random.NextDouble() * .1f;
                var cover = i % 4 == 0 ? green : i % 4 == 1 ? leather : i % 4 == 2 ? panel : darkWood;
                float x = start + i * .067f;
                var book = Group("Livro " + i, new Vector3(x, level, 0), shelf);
                Box("Miolo", new Vector3(0, height * .5f, 0), new Vector3(width, height, .2f), paper, book);
                foreach (float side in new[] { -1f, 1f }) Box("Capa", new Vector3(side * (width * .5f + .003f), height * .5f, 0), new Vector3(.006f, height + .008f, .207f), cover, book);
                Box("Lombada", new Vector3(0, height * .5f, -.104f), new Vector3(width + .007f, height + .008f, .01f), cover, book);
                foreach (float y in new[] { .035f, height - .035f }) Box("Filete dourado", new Vector3(0, y, -.110f), new Vector3(width * .8f, .003f, .001f), brass, book);
            }
        }

        static void Shelf(Vector3 pos, float yaw)
        {
            var s = Group("Estante de investigações", pos, null, yaw);
            Box("Fundo", new Vector3(0, 1.08f, .22f), new Vector3(1.72f, 2.16f, .04f), darkWood, s);
            foreach (float x in new[] { -.85f, .85f }) Box("Lateral", new Vector3(x, 1.08f, 0), new Vector3(.055f, 2.16f, .5f), timber, s);
            for (int i = 0; i < 6; i++) Box("Prateleira", new Vector3(0, .1f + i * .41f, 0), new Vector3(1.74f, .05f, .51f), timber, s);
            for (int i = 0; i < 5; i++) Books(s, .135f + i * .41f, i == 2 ? 7 : 19, -.74f);
            Collision(s, new Vector3(0, 1.1f, 0), new Vector3(1.8f, 2.2f, .52f));
        }

        static void Chair(Vector3 pos, float yaw, bool office)
        {
            var c = Group(office ? "Cadeira de João" : "Cadeira de visitante", pos, null, yaw);
            Box("Assento acolchoado", new Vector3(0, .46f, 0), new Vector3(.52f, .095f, .48f), leather, c);
            Box("Encosto", new Vector3(0, .86f, .23f), new Vector3(.5f, .58f, .09f), leather, c, false, new Vector3(-7, 0, 0));
            for (int i = -1; i <= 1; i += 2)
            {
                Rod("Suporte do encosto", new Vector3(i * .19f, .45f, .2f), new Vector3(i * .19f, .96f, .28f), .018f, steel, c);
                Rod("Braço", new Vector3(i * .3f, .68f, -.15f), new Vector3(i * .3f, .68f, .19f), .028f, timber, c);
                Rod("Suporte do braço", new Vector3(i * .29f, .44f, .05f), new Vector3(i * .29f, .68f, .05f), .015f, steel, c);
            }
            if (office)
            {
                Rod("Pistão", new Vector3(0, .07f, 0), new Vector3(0, .41f, 0), .044f, steel, c);
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * Mathf.PI * 2 / 5;
                    Vector3 end = new Vector3(Mathf.Sin(angle) * .32f, .075f, Mathf.Cos(angle) * .32f);
                    Rod("Base estrela", new Vector3(0, .12f, 0), end, .025f, black, c);
                    Round("Rodinha", end - Vector3.up * .027f, new Vector3(.07f, .028f, .07f), black, c);
                }
            }
            else foreach (float x in new[] { -.2f, .2f }) foreach (float z in new[] { -.17f, .17f }) Rod("Pé", new Vector3(x, .02f, z), new Vector3(x, .43f, z), .025f, timber, c);
            Collision(c, new Vector3(0, .55f, .045f), new Vector3(.61f, 1.1f, .61f));
        }

        static void Desk()
        {
            var d = Group("Mesa principal — João de Claus", new Vector3(0, 0, 1.12f));
            Box("Tampo de madeira", new Vector3(0, .79f, 0), new Vector3(2.14f, .075f, .99f), timber, d);
            Box("Borda frontal", new Vector3(0, .748f, -.49f), new Vector3(2.14f, .05f, .025f), darkWood, d);
            foreach (float x in new[] { -.8f, .8f })
            {
                Box("Gaveteiro", new Vector3(x, .37f, .08f), new Vector3(.49f, .72f, .79f), darkWood, d);
                for (int i = 0; i < 3; i++)
                {
                    var drawer = Group("Gaveta " + x + "/" + i, new Vector3(x, .2f + i * .205f, -.327f), d);
                    Box("Frente entalhada", Vector3.zero, new Vector3(.465f, .185f, .035f), timber, drawer);
                    Box("Painel rebaixado", new Vector3(0, 0, -.021f), new Vector3(.37f, .115f, .012f), darkWood, drawer);
                    Rod("Puxador latão", new Vector3(-.065f, 0, -.056f), new Vector3(.065f, 0, -.056f), .009f, brass, drawer);
                    if (x > 0 && i == 2)
                    {
                        drawer.gameObject.layer = interact;
                        var col = drawer.gameObject.AddComponent<BoxCollider>(); col.size = new Vector3(.465f, .185f, .16f);
                        Box("Fundo da gaveta", new Vector3(0, -.08f, .25f), new Vector3(.43f, .025f, .5f), darkWood, drawer);
                        var script = drawer.gameObject.AddComponent<InteractiveDrawer>(); script.drawer = drawer; script.localTravel = new Vector3(0, 0, -.34f);
                        script.displayName = "gaveta da mesa"; script.persistentId = Guid.NewGuid().ToString("N");
                        script.sound = AssetDatabase.LoadAssetAtPath<SoundSet>("Assets/HorrorGameplay/Audio/Door creak.asset");
                    }
                }
                Collision(d, new Vector3(x, .37f, .08f), new Vector3(.5f, .74f, .8f));
            }
            Collision(d, new Vector3(0, .78f, 0), new Vector3(2.18f, .08f, 1));
            Box("Base de couro para escrita", new Vector3(-.08f, .832f, -.16f), new Vector3(.8f, .014f, .42f), leather, d);
            for (int i = 0; i < 5; i++) Page(d, new Vector3(-.57f + i * .017f, .84f + i * .003f, .08f), -17 + i * 8, i == 4 ? "ANOTAÇÕES DO CASO" : "RELATÓRIO");
            Page(d, new Vector3(.1f, .841f, -.22f), 7, "JOÃO DE CLAUS", true, "Investigações particulares\n\nDesaparecimentos. Arquivos antigos. Perguntas que ninguém quis responder.\n\nAtendimento somente com horário marcado.");
            Rod("Caneta", new Vector3(.46f, .843f, -.23f), new Vector3(.6f, .843f, -.13f), .006f, black, d);
            var cup = Round("Caneca de café", new Vector3(.65f, .885f, .02f), new Vector3(.09f, .055f, .09f), cream, d);
            Round("Café frio", new Vector3(.65f, .941f, .02f), new Vector3(.077f, .0008f, .077f), black, d);
            Rod("Alça da caneca", new Vector3(.7f, .86f, .02f), new Vector3(.725f, .917f, .02f), .009f, cream, d);
            Computer(d); Telephone(d); DeskLamp(d);
            var plaque = Group("Identificação do investigador", new Vector3(-.15f, .9f, -.46f), d);
            Box("Plaqueta", Vector3.zero, new Vector3(.48f, .09f, .025f), brass, plaque);
            Text("Nome", "JOÃO DE CLAUS", new Vector3(0, 0, -.014f), Vector3.zero, new Vector2(.46f, .08f), 4, plaque);
            Chair(new Vector3(0, 0, 2.2f), 0, true);
            Chair(new Vector3(-.73f, 0, -.3f), 172, false); Chair(new Vector3(.8f, 0, -.3f), 192, false);
        }

        static void Computer(Transform desk)
        {
            var pc = Group("Computador e teclado", new Vector3(-.49f, .835f, .22f), desk, -7);
            Box("Monitor CRT", new Vector3(0, .2f, .04f), new Vector3(.4f, .35f, .34f), cream, pc);
            Box("Moldura preta", new Vector3(0, .22f, -.14f), new Vector3(.34f, .265f, .018f), black, pc);
            Box("Tela acesa", new Vector3(0, .22f, -.151f), new Vector3(.30f, .23f, .003f), glow, pc);
            Text("Texto na tela", "ARQUIVO DE CASOS\n\n> AGUARDANDO...", new Vector3(0, .22f, -.154f), Vector3.zero, new Vector2(.28f, .2f), 2.8f, pc, new Color(.31f, .75f, .58f));
            Box("Base do monitor", new Vector3(0, .027f, 0), new Vector3(.32f, .05f, .31f), cream, pc);
            Box("Teclado", new Vector3(.07f, .026f, -.35f), new Vector3(.39f, .033f, .135f), cream, pc);
            for (int y = 0; y < 4; y++) for (int x = 0; x < 13; x++) Box("Tecla", new Vector3(-.1f + x * .027f, .047f, -.396f + y * .027f), new Vector3(.022f, .008f, .021f), paper, pc);
            Light("Reflexo do monitor", new Vector3(0, .25f, -.23f), new Color(.36f, .6f, .51f), .7f, 1.5f, pc);
            var tower = Group("Gabinete do computador", new Vector3(-1.27f, -.45f, .1f), desk);
            Box("Gabinete", Vector3.zero, new Vector3(.22f, .43f, .4f), cream, tower);
            for (int i = 0; i < 6; i++) Box("Ventilação", new Vector3(0, -.08f + i * .024f, -.202f), new Vector3(.16f, .005f, .002f), black, tower);
        }

        static void Telephone(Transform desk)
        {
            var p = Group("Telefone — peça central do capítulo", new Vector3(.76f, .837f, .22f), desk, 9);
            var visual = Group("Built-in telephone visual", Vector3.zero, p);
            Box("Base do telefone", new Vector3(0, .04f, 0), new Vector3(.25f, .08f, .21f), black, visual);
            var dial = Round("Disco rotativo", new Vector3(0, .09f, -.025f), new Vector3(.115f, .008f, .115f), brass, visual);
            Round("Centro do disco", new Vector3(0, .1f, -.025f), new Vector3(.042f, .003f, .042f), paper, visual);
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2 / 11;
                Round("Furo do disco", new Vector3(Mathf.Sin(a) * .044f, .1f, -.025f + Mathf.Cos(a) * .044f), new Vector3(.017f, .001f, .017f), black, visual);
            }
            Rod("Haste do fone", new Vector3(-.1f, .135f, .065f), new Vector3(.1f, .135f, .065f), .021f, black, visual);
            foreach (float x in new[] { -.11f, .11f }) Round("Auricular", new Vector3(x, .125f, .065f), new Vector3(.055f, .033f, .065f), black, visual, true);
            Vector3 last = new Vector3(.12f, .13f, .065f);
            for (int i = 1; i <= 72; i++)
            {
                float t = i / 72f;
                Vector3 next = new Vector3(.14f + t * .11f, .085f - t * .06f + Mathf.Sin(t * 36 * Mathf.PI) * .009f, .07f + Mathf.Cos(t * 36 * Mathf.PI) * .012f);
                Rod("Cabo espiral", last, next, .0023f, black, visual); last = next;
            }
            Collision(p, new Vector3(0, .07f, 0), new Vector3(.29f, .17f, .24f));
        }

        static void DeskLamp(Transform desk)
        {
            var l = Group("Luminária de mesa", new Vector3(-.87f, .84f, .26f), desk);
            Round("Base pesada", new Vector3(0, .018f, 0), new Vector3(.21f, .018f, .17f), brass, l);
            Rod("Haste", new Vector3(0, .03f, 0), new Vector3(0, .4f, 0), .011f, brass, l);
            Box("Cúpula verde", new Vector3(0, .39f, -.04f), new Vector3(.29f, .085f, .15f), green, l);
            Box("Difusor", new Vector3(0, .347f, -.04f), new Vector3(.245f, .008f, .115f), glow, l);
            Light("Luz quente da mesa", new Vector3(0, .31f, -.07f), new Color(1, .72f, .38f), 8, 3.8f, l);
        }

        static void Window()
        {
            var w = Group("Janela para a rua — persianas", new Vector3(3.87f, 1.7f, .9f), null, 90);
            Box("Escuridão externa", new Vector3(0, 0, -.01f), new Vector3(2.38f, 1.74f, .03f), black, w);
            foreach (float x in new[] { -1.2f, 1.2f }) Box("Montante", new Vector3(x, 0, 0), new Vector3(.08f, 1.9f, .12f), timber, w);
            foreach (float y in new[] { -.91f, .91f }) Box("Batente", new Vector3(0, y, 0), new Vector3(2.47f, .08f, .12f), timber, w);
            Box("Peitoril", new Vector3(0, -.9f, -.08f), new Vector3(2.55f, .06f, .31f), timber, w);
            for (int i = 0; i < 19; i++) Box("Lâmina da persiana", new Vector3(0, -.82f + i * .091f, -.045f), new Vector3(2.3f, .028f, .072f), steel, w, false, new Vector3(24, 0, 0));
            foreach (float x in new[] { -.78f, .78f }) Rod("Cordão da persiana", new Vector3(x, -.82f, -.1f), new Vector3(x, .82f, -.1f), .003f, paper, w);
            var moon = Light("Luz fria da rua", new Vector3(3.4f, 2.15f, .95f), new Color(.46f, .62f, .89f), 15, 8, null, LightType.Spot, new Vector3(18, -90, 0));
            moon.spotAngle = 112; moon.innerSpotAngle = 77;
            for (int i = 0; i < 16; i++)
            {
                var stripe = Box("Faixa de luz da persiana", new Vector3(2.43f - i * .065f, .004f, .65f + i * .09f), new Vector3(2.7f, .002f, .025f), glow, null, false, new Vector3(0, -12, 0));
            }
        }

        static void EvidenceBoard()
        {
            var b = Group("Mural de casos em aberto", new Vector3(-3.87f, 1.72f, 1.13f), null, -90);
            Box("Moldura", Vector3.zero, new Vector3(2.0f, 1.24f, .07f), timber, b);
            Box("Cortiça", new Vector3(0, 0, -.04f), new Vector3(1.87f, 1.10f, .015f), cork, b);
            Text("Cabeçalho do mural", "CASOS EM ABERTO", new Vector3(0, .42f, -.052f), Vector3.zero, new Vector2(1.5f, .18f), 7, b, new Color(.62f, .56f, .4f));
            var poses = new[] { new Vector2(-.66f,.13f),new Vector2(-.22f,.10f),new Vector2(.24f,.08f),new Vector2(.65f,.13f),new Vector2(-.38f,-.29f),new Vector2(.27f,-.27f) };
            for (int i = 0; i < poses.Length; i++)
            {
                var p = poses[i];
                Box("Recorte e fotografia " + i, new Vector3(p.x, p.y, -.055f), new Vector3(.29f, .29f, .004f), paper, b, false, new Vector3(0, 0, i * 3 - 8));
                if (i < 4)
                {
                    Box("Foto sépia", new Vector3(p.x, p.y + .035f, -.06f), new Vector3(.23f, .16f, .002f), panel, b);
                    Round("Silhueta na foto", new Vector3(p.x, p.y + .07f, -.064f), new Vector3(.061f, .04f, .025f), darkWood, b, true);
                    Box("Corpo na foto", new Vector3(p.x, p.y + .015f, -.065f), new Vector3(.09f, .07f, .002f), darkWood, b);
                }
                Text("Legenda", i < 4 ? "DESAPARECIDO" : "SEM RESPOSTA", new Vector3(p.x, p.y - .10f, -.065f), Vector3.zero, new Vector2(.28f, .05f), 1.9f, b);
                Round("Alfinete", new Vector3(p.x, p.y + .14f, -.068f), new Vector3(.015f, .015f, .015f), brass, b, true);
                if (i > 0) Rod("Linha de investigação", new Vector3(poses[i-1].x, poses[i-1].y, -.075f), new Vector3(p.x, p.y, -.075f), .0018f, leather, b);
            }
        }

        static void Archive(Vector3 pos)
        {
            var f = Group("Arquivo de metal", pos);
            Box("Carcaça", new Vector3(0, .71f, 0), new Vector3(.56f, 1.42f, .61f), steel, f);
            for (int i = 0; i < 4; i++)
            {
                float y = .20f + i * .335f;
                Box("Gaveta do arquivo", new Vector3(0, y, -.313f), new Vector3(.5f, .3f, .018f), panel, f);
                Rod("Puxador", new Vector3(-.065f, y + .035f, -.345f), new Vector3(.065f, y + .035f, -.345f), .009f, steel, f);
                Box("Etiqueta", new Vector3(0, y - .047f, -.327f), new Vector3(.16f, .05f, .003f), paper, f);
                Text("Identificação", new[] { "ENCERRADOS", "DOCUMENTOS", "DESAPARECIDOS", "PENDENTES" }[i], new Vector3(0, y-.047f, -.331f), Vector3.zero, new Vector2(.15f,.045f), 1.4f, f);
            }
            Collision(f, new Vector3(0,.71f,0), new Vector3(.57f,1.43f,.64f));
            Box("Caixa de arquivo", new Vector3(0, 1.57f, 0), new Vector3(.5f,.28f,.5f), cork, f);
        }

        static void Door()
        {
            var d = Group("Porta do escritório", new Vector3(-2.63f, 0, -3.43f));
            foreach(float x in new[]{0f,1.1f}) Box("Batente",new Vector3(x,1.11f,0),new Vector3(.10f,2.25f,.19f),darkWood,d,true);
            Box("Verga",new Vector3(.55f,2.23f,0),new Vector3(1.22f,.10f,.19f),darkWood,d,true);
            var h=Group("Dobradiça",new Vector3(.05f,0,0),d);
            var leaf=Box("Folha de madeira",new Vector3(.5f,1.08f,0),new Vector3(1.0f,2.16f,.055f),timber,h,true);
            Box("Vidro fosco",new Vector3(.5f,1.56f,-.032f),new Vector3(.73f,.79f,.012f),glass,h);
            Text("Nome no vidro","JOÃO DE CLAUS\nINVESTIGAÇÕES",new Vector3(.5f,1.58f,.035f),new Vector3(0,180,0),new Vector2(.7f,.43f),7,h,new Color(.78f,.73f,.58f));
            Rod("Maçaneta",new Vector3(.8f,.94f,.08f),new Vector3(.94f,.94f,.08f),.014f,brass,h);
            for(int i=0;i<2;i++)Box("Painel da porta",new Vector3(.5f,.32f+i*.47f,.035f),new Vector3(.77f,.35f,.019f),darkWood,h);
            d.gameObject.layer=interact; foreach(var c in h.GetComponentsInChildren<Collider>())c.gameObject.layer=interact;
            var door=d.gameObject.AddComponent<InteractiveDoor>();door.hinge=h;door.leafCollider=leaf.GetComponent<BoxCollider>();door.openAngle=-91;door.speed=70;door.displayName="porta do escritório";door.playerMask=1<<LayerMask.NameToLayer("Player");door.persistentId=Guid.NewGuid().ToString("N");door.creak=AssetDatabase.LoadAssetAtPath<SoundSet>("Assets/HorrorGameplay/Audio/Door creak.asset");
            var hall=Group("Vestíbulo externo",new Vector3(-2.08f,0,-4.35f));
            Box("Piso",new Vector3(0,-.08f,0),new Vector3(1.52f,.16f,1.75f),timber,hall,true);
            Box("Parede esquerda",new Vector3(-.77f,1.5f,0),new Vector3(.15f,3,1.75f),panel,hall,true);
            Box("Parede direita",new Vector3(.77f,1.5f,0),new Vector3(.15f,3,1.75f),panel,hall,true);
            Box("Fundo",new Vector3(0,1.5f,-.89f),new Vector3(1.65f,3,.15f),panel,hall,true);
            Box("Teto",new Vector3(0,3,0),new Vector3(1.6f,.14f,1.9f),panel,hall,true);
            Light("Luz do vestíbulo",new Vector3(0,2.7f,-.12f),new Color(.9f,.76f,.52f),2.3f,3,hall);
        }

        static void Clock()
        {
            var c=Group("Relógio de parede",new Vector3(1.7f,2.3f,3.55f));
            var frame=Round("Moldura circular",Vector3.zero,new Vector3(.42f,.025f,.42f),darkWood,c);frame.transform.localRotation=Quaternion.Euler(90,0,0);
            var face=Round("Mostrador",new Vector3(0,0,-.026f),new Vector3(.365f,.001f,.365f),paper,c);face.transform.localRotation=Quaternion.Euler(90,0,0);
            for(int i=0;i<12;i++){float a=i*Mathf.PI/6;Box("Marca da hora",new Vector3(Mathf.Sin(a)*.16f,Mathf.Cos(a)*.16f,-.029f),new Vector3(.008f,.025f,.002f),black,c,false,new Vector3(0,0,-i*30));}
            Rod("Ponteiro das horas",new Vector3(0,0,-.034f),new Vector3(-.06f,.065f,-.034f),.006f,black,c);
            Rod("Ponteiro dos minutos",new Vector3(0,0,-.038f),new Vector3(.07f,.11f,-.038f),.003f,black,c);
        }

        public static string Create()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first.");
            if(File.Exists(ScenePath))throw new InvalidOperationException("Office already exists; use targeted edits.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save current scene first.");
            foreach(var folder in new[]{"Scenes","Materials","Textures","Models","Documentation"})Directory.CreateDirectory(Root+folder);
            AssetDatabase.Refresh();world=LayerMask.NameToLayer("World");interact=LayerMask.NameToLayer("Interactable");serial=0;
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/HorrorMenu/TMP/Resources/Fonts & Materials/LiberationSans SDF.asset");
            plaster=Mat("Plaster — worn ochre",new Color(.48f,.45f,.37f));panel=Mat("Muted olive paint",new Color(.23f,.27f,.22f));
            timber=Mat("Walnut grain",new Color(.31f,.19f,.105f),1,.28f);darkWood=Mat("Dark walnut",new Color(.13f,.079f,.045f),1,.22f);
            steel=Mat("Aged gunmetal",new Color(.20f,.22f,.21f),0,.4f,.6f);brass=Mat("Tarnished brass",new Color(.55f,.40f,.17f),0,.43f,.75f);
            leather=Mat("Brown leather",new Color(.22f,.095f,.059f),2,.27f);paper=Mat("Aged paper",new Color(.78f,.73f,.58f));cream=Mat("Ivory plastic",new Color(.54f,.53f,.43f),0,.25f);
            black=Mat("Black bakelite",new Color(.035f,.04f,.035f),0,.43f);rug=Mat("Faded woven rug",new Color(.27f,.12f,.073f),2,.08f);
            glass=Mat("Smoked frosted glass",new Color(.17f,.20f,.16f),0,.62f);green=Mat("Lamp green enamel",new Color(.057f,.19f,.09f),0,.7f);cork=Mat("Aged cork and cardboard",new Color(.45f,.30f,.18f));
            glow=new Material(Shader.Find("Universal Render Pipeline/Unlit"));glow.SetColor("_BaseColor",new Color(.075f,.15f,.13f));AssetDatabase.CreateAsset(glow,Root+"Materials/Dim screen.mat");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);room=new GameObject("Escritório de João de Claus — Capítulo 1").transform;
            var floor=Box("Piso com colisão",new Vector3(0,-.10f,.1f),new Vector3(8,.2f,7.3f),timber,null,true);floor.AddComponent<SurfaceType>().surface=GroundSurface.Wood;
            for(int row=0;row<31;row++)for(int col=0;col<4;col++)Box("Tábua de assoalho",new Vector3(-3+col*2+(row%2)*.23f,.002f,-3.43f+row*.231f),new Vector3(1.97f,.004f,.224f),timber).GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            Box("Parede do fundo",new Vector3(0,1.52f,3.66f),new Vector3(8.1f,3.04f,.18f),plaster,null,true);
            Box("Parede esquerda",new Vector3(-3.96f,1.52f,.1f),new Vector3(.18f,3.04f,7.3f),plaster,null,true);
            Box("Parede direita",new Vector3(3.96f,1.52f,.1f),new Vector3(.18f,3.04f,7.3f),plaster,null,true);
            Box("Parede de entrada esquerda",new Vector3(-3.32f,1.52f,-3.53f),new Vector3(1.28f,3.04f,.18f),plaster,null,true);
            Box("Parede de entrada direita",new Vector3(1.23f,1.52f,-3.53f),new Vector3(5.46f,3.04f,.18f),plaster,null,true);
            Box("Acima da porta",new Vector3(-2.08f,2.66f,-3.53f),new Vector3(1.16f,.76f,.18f),plaster,null,true);
            Box("Teto",new Vector3(0,3.11f,.1f),new Vector3(8.15f,.17f,7.5f),plaster,null,true);
            foreach(float x in new[]{-3.84f,3.84f}){Box("Rodapé",new Vector3(x,.11f,.1f),new Vector3(.07f,.22f,7.3f),darkWood);Box("Friso",new Vector3(x,1.0f,.1f),new Vector3(.065f,.05f,7.3f),timber);Box("Lambril",new Vector3(x,.5f,.1f),new Vector3(.04f,.82f,7.3f),panel);}
            Box("Rodapé do fundo",new Vector3(0,.11f,3.55f),new Vector3(7.7f,.22f,.06f),darkWood);
            Box("Tapete central",new Vector3(0,.012f,.45f),new Vector3(2.6f,.017f,2.8f),rug);
            for(int i=0;i<4;i++)Box("Borda do tapete",new Vector3(i<2?(i==0?-1.23f:1.23f):0,.022f,i<2?.45f:(i==2?-.88f:1.78f)),i<2?new Vector3(.035f,.002f,2.66f):new Vector3(2.5f,.002f,.035f),paper);
            Desk();Shelf(new Vector3(-2.49f,0,3.32f),0);Shelf(new Vector3(2.66f,0,3.3f),0);Window();EvidenceBoard();Archive(new Vector3(-3.47f,0,-1.38f));Archive(new Vector3(-2.79f,0,-1.36f));Door();Clock();
            var side=Group("Mesa auxiliar de arquivos",new Vector3(3.37f,0,-1.6f),null,-90);
            Box("Tampo",new Vector3(0,.69f,0),new Vector3(1.2f,.06f,.56f),timber,side);
            foreach(float x in new[]{-.5f,.5f})foreach(float z in new[]{-.2f,.2f})Box("Pé",new Vector3(x,.34f,z),new Vector3(.035f,.68f,.035f),darkWood,side);
            Collision(side,new Vector3(0,.37f,0),new Vector3(1.23f,.75f,.59f));
            Page(side,new Vector3(.24f,.729f,0),-6,"AGENDA",true,"Escritório de João de Claus\n\nOrganizar os arquivos. Conferir as ligações recebidas. Revisar os casos pendentes.\n\nAlgumas perguntas continuam sem resposta.");
            for(int i=0;i<7;i++)Box("Pasta de arquivo",new Vector3(-.34f,.74f+i*.025f,.04f),new Vector3(.32f,.019f,.23f),i%2==0?cork:paper,side,false,new Vector3(0,i*3-10,0));
            var stand=Group("Cabideiro e casaco",new Vector3(2.88f,0,-2.66f));Rod("Haste do cabideiro",new Vector3(0,.05f,0),new Vector3(0,1.83f,0),.027f,darkWood,stand);
            for(int i=0;i<4;i++){float a=i*Mathf.PI/2;Rod("Gancho",new Vector3(0,1.65f,0),new Vector3(Mathf.Sin(a)*.24f,1.78f,Mathf.Cos(a)*.24f),.018f,timber,stand);Rod("Base",new Vector3(0,.07f,0),new Vector3(Mathf.Sin(a)*.3f,.015f,Mathf.Cos(a)*.3f),.019f,darkWood,stand);}
            Box("Casaco de investigação",new Vector3(.12f,1.04f,.13f),new Vector3(.37f,.98f,.10f),leather,stand,false,new Vector3(0,13,-5));
            Rod("Manga do casaco",new Vector3(-.07f,1.47f,.13f),new Vector3(-.16f,.91f,.13f),.065f,leather,stand);
            Round("Lixeira",new Vector3(1.36f,.20f,1.73f),new Vector3(.29f,.20f,.29f),steel,room);
            for(int i=0;i<8;i++)Round("Papel amassado",new Vector3(1.36f+Mathf.Sin(i)*.085f,.39f+(i%2)*.015f,1.73f+Mathf.Cos(i)*.085f),new Vector3(.064f,.05f,.065f),paper,room,true);
            var calendar=Group("Calendário e recibos",new Vector3(2.56f,1.72f,-3.40f),null,180);
            Box("Folha do calendário",Vector3.zero,new Vector3(.48f,.57f,.006f),paper,calendar);
            Text("Calendário","OUTUBRO\n\nSEG  TER  QUA  QUI  SEX\n\n  1      2      3      4      5\n  8      9     10    11    12\n15    16    17    18    19\n22    23    24    25    26",new Vector3(0,0,-.006f),Vector3.zero,new Vector2(.44f,.54f),3,calendar);
            Light("Luz suave do teto",new Vector3(.4f,2.77f,-.6f),new Color(1,.78f,.51f),7.5f,8);
            Round("Rosácea do plafon",new Vector3(.4f,3.0f,-.6f),new Vector3(.28f,.025f,.28f),brass,room);
            Round("Globo de vidro",new Vector3(.4f,2.88f,-.6f),new Vector3(.28f,.20f,.28f),paper,room,true);
            Light("Preenchimento discreto",new Vector3(-2.2f,2.35f,-.1f),new Color(.75f,.73f,.60f),2.1f,6);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.13f,.14f,.15f);RenderSettings.skybox=null;RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.008f;RenderSettings.fogColor=new Color(.075f,.084f,.09f);
            var gameplay=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HorrorGameplay/Prefabs/GameplayRoot.prefab"));
            var session=gameplay.GetComponent<GameSession>();session.player.transform.SetPositionAndRotation(new Vector3(-1.28f,.04f,-2.45f),Quaternion.Euler(0,14,0));
            var intro=new SerializedObject(session);var objective=intro.FindProperty("initialObjective");if(objective!=null){objective.stringValue="Explore o escritório de João de Claus.";intro.ApplyModifiedPropertiesWithoutUndo();}
            PrefabUtility.RecordPrefabInstancePropertyModifications(session.player.transform);
            var flashlight=session.player.GetComponent<FlashlightController>();flashlight.startsOn=false;PrefabUtility.RecordPrefabInstancePropertyModifications(flashlight);
            foreach(var c in room.GetComponentsInChildren<CollectibleItem>())EditorUtility.SetDirty(c);
            foreach(var r in room.GetComponentsInChildren<Renderer>())if(!r.GetComponentInParent<InteractiveDoor>()&&!r.GetComponentInParent<InteractiveDrawer>()&&!r.GetComponentInParent<CollectibleItem>())GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.BatchingStatic);
            EditorSceneManager.SaveScene(scene,ScenePath);
            DetailScene();
            var scenes=EditorBuildSettings.scenes.ToList();if(!scenes.Any(s=>s.path==ScenePath))scenes.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();
            return "Office saved with " + room.GetComponentsInChildren<Renderer>().Length + " detailed renderers, colliders, notes, an interactive drawer and an opening door.";
        }
    }
}

