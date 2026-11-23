// Ferramenta de montagem da estrada, do carro ou do rádio.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorGame.Editor
{
    // A modest, well-used sedan. All artwork and its persistent assets are made in the Editor.
    public static class DetectiveSedanBuilder
    {
        const string Assets = "Assets/RoadChapter/Models/DetectiveSedan";
        static Material paint, chrome, steel, rubber, dash, cloth, clothSeam, liner, glassMirror;
        static Material cream, headlamp, amber, red, paper, leather, instrument, needle, dark;
        static Transform exterior, interior, driverDoor;

        public static Transform Build(Transform parent)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            Folders(Assets);
            MakeMaterials();
            var model = Group("Sedã de João — carro comum de investigação", parent);
            exterior = Group("Exterior", model);
            interior = Group("Cabine", model);
            driverDoor = Group("DriverDoorVisual", model);
            Body();
            Wheels();
            Cabin();
            Dashboard();
            Seats();
            Props();
            Merge(exterior, "Exterior");
            Merge(interior, "Cabin");
            Merge(driverDoor, "DriverDoor");
            AssetDatabase.SaveAssets();
            return model;
        }

        static void MakeMaterials()
        {
            paint = Mat("OlivePaint", new Color(.115f, .139f, .112f), .45f, .42f);
            chrome = Mat("OldChrome", new Color(.56f, .57f, .53f), .88f, .58f);
            steel = Mat("Steel", new Color(.23f, .245f, .22f), .68f, .32f);
            rubber = Mat("Rubber", new Color(.017f, .018f, .016f), 0, .14f);
            dark = Mat("DarkRecess", new Color(.009f, .012f, .010f), 0, .06f);
            dash = Mat("DashboardVinyl", new Color(.064f, .061f, .051f), 0, .20f);
            cloth = Mat("SeatWovenCloth", new Color(.185f, .181f, .151f), 0, .05f);
            clothSeam = Mat("UpholsterySeams", new Color(.077f, .082f, .069f), 0, .12f);
            liner = Mat("Headliner", new Color(.36f, .346f, .29f), 0, .02f);
            glassMirror = Mat("Mirror", new Color(.32f, .37f, .36f), .88f, .75f);
            cream = Mat("Ivory", new Color(.68f, .655f, .52f), 0, .20f);
            headlamp = Mat("HeadlampGlass", new Color(.72f, .73f, .59f), .05f, .38f, new Color(1.2f, 1.15f, .86f));
            amber = Mat("WarmLens", new Color(.78f, .57f, .22f), .05f, .32f, new Color(.23f, .14f, .04f));
            red = Mat("TailLamp", new Color(.37f, .026f, .018f), .05f, .34f, new Color(.04f, .003f, .001f));
            paper = Mat("AgedPaper", new Color(.56f, .52f, .37f), 0, .04f);
            leather = Mat("CaseLeather", new Color(.086f, .066f, .041f), 0, .25f);
            instrument = Mat("GaugeScale", new Color(.72f, .74f, .63f), 0, .10f, new Color(.035f, .04f, .025f));
            needle = Mat("GaugeNeedle", new Color(.75f, .39f, .15f), 0, .16f, new Color(.055f, .015f, .001f));
            cloth.SetTexture("_BaseMap", WovenTexture());
            EditorUtility.SetDirty(cloth);
        }

        static Material Mat(string key, Color color, float metallic, float smoothness, Color? emission = null)
        {
            string path = Assets + "/" + key + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.name = key;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Cull", (float)CullMode.Back);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            else mat.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D WovenTexture()
        {
            const int size = 128;
            string path = Assets + "/WovenCloth.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture)
            {
                texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
                texture.name = "Small woven seat fabric";
                AssetDatabase.CreateAsset(texture, path);
            }
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float thread = ((x % 4 == 0) ^ (y % 4 == 0)) ? .71f : .93f;
                float speckle = ((x * 31 + y * 17) % 13) * .005f;
                pixels[y * size + x] = new Color(thread + speckle, thread + speckle, thread + speckle, 1);
            }
            texture.SetPixels(pixels);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            texture.Apply(true, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        static void Body()
        {
            Box("Floor pan", exterior, V(0, .395f, -.05f), V(1.70f, .10f, 4.30f), rubber, .025f);
            Box("Engine bay", exterior, V(0, .64f, 1.64f), V(1.66f, .42f, 1.18f), paint, .075f);
            Box("Trunk volume", exterior, V(0, .68f, -1.97f), V(1.68f, .43f, .68f), paint, .065f);
            MeshObject("Sloping hood", exterior, Hood(), paint);
            Box("Trunk lid", exterior, V(0, .927f, -1.965f), V(1.77f, .074f, .70f), paint, .028f);
            Line("Hood left seam", exterior, new[] { V(-.817f, .943f, .90f), V(-.83f, .914f, 2.19f) }, .003f, dark);
            Line("Hood right seam", exterior, new[] { V(.817f, .943f, .90f), V(.83f, .914f, 2.19f) }, .003f, dark);
            Line("Trunk lid seam", exterior, new[] { V(-.80f, .967f, -1.66f), V(-.82f, .967f, -2.25f), V(.82f, .967f, -2.25f), V(.80f, .967f, -1.66f) }, .003f, dark);
            for (int side = -1; side <= 1; side += 2)
            {
                MeshObject("Wheel arch body " + side, exterior, BodySide(side), paint);
                Box("Rocker sill " + side, exterior, V(side * .898f, .43f, -.02f), V(.095f, .13f, 2.33f), paint, .025f);
                Box("Chrome sill line " + side, exterior, V(side * .949f, .48f, -.02f), V(.008f, .025f, 2.29f), chrome, .003f);
                for (int axle = -1; axle <= 1; axle += 2)
                {
                    var arch = new List<Vector3>();
                    for (int i = 0; i <= 20; i++)
                    {
                        float a = Mathf.PI * i / 20;
                        arch.Add(V(side * .956f, .34f + Mathf.Sin(a) * .382f, axle * 1.55f + Mathf.Cos(a) * .382f));
                    }
                    Line("Wheel arch lip", exterior, arch.ToArray(), .012f, paint, 6);
                    Line("Wheel arch shadow", exterior, arch.Select(p => p + V(side * .002f, -.016f, 0)).ToArray(), .006f, rubber, 6);
                }
                Box("Body side protection strip", exterior, V(side * .958f, .686f, 0), V(.02f, .066f, 3.91f), rubber, .014f);
                Box("Side protection chrome insert", exterior, V(side * .972f, .70f, 0), V(.008f, .015f, 3.88f), chrome, .003f);
                // Front and rear doors are distinct conventional panels with narrow dark gaps.
                foreach (float z in new[] { .81f, -.48f, -1.36f })
                    Line("Vertical door gap", exterior, new[] { V(side * .954f, .946f, z), V(side * .960f, .765f, z), V(side * .950f, .505f, z + .025f) }, .0032f, dark);
                for (int door = 0; door < 2; door++)
                {
                    float z = door == 0 ? -.28f : -1.12f;
                    var group = side == -1 && door == 0 ? driverDoor : exterior;
                    Box("Exterior door handle recess", group, V(side * .966f, .891f, z), V(.016f, .043f, .155f), dark, .010f);
                    Box("Exterior chrome pull", group, V(side * .982f, .902f, z), V(.028f, .022f, .135f), chrome, .008f);
                    Disc("Door lock cylinder", group, V(side * .980f, .858f, z + .035f), .010f, .004f, steel, Quaternion.Euler(0, side * 90, 0));
                }
                var mirror = Group("Side mirror " + side, exterior);
                Box("Mirror stem", mirror, V(side * .949f, 1.036f, .706f), V(.18f, .029f, .045f), rubber, .009f);
                Box("Mirror housing", mirror, V(side * 1.034f, 1.076f, .674f), V(.134f, .102f, .18f), paint, .025f);
                Box("Side mirror face", mirror, V(side * 1.034f, 1.078f, .578f), V(.108f, .076f, .008f), glassMirror, .009f);
            }
            FrontAndRear();
        }

        static void FrontAndRear()
        {
            Box("Front header panel", exterior, V(0, .743f, 2.294f), V(1.80f, .355f, .096f), paint, .035f);
            Box("Grille chrome surround", exterior, V(0, .771f, 2.352f), V(.976f, .224f, .035f), chrome, .018f);
            Box("Grille dark inset", exterior, V(0, .771f, 2.374f), V(.930f, .178f, .016f), dark, .008f);
            for (int i = 0; i < 15; i++)
                Box("Vertical grille bar", exterior, V(-.427f + i * .061f, .771f, 2.389f), V(.012f, .162f, .009f), chrome, .003f);
            for (int i = 0; i < 3; i++)
                Box("Horizontal grille bar", exterior, V(0, .723f + i * .049f, 2.391f), V(.922f, .010f, .012f), steel, .002f);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Paired rectangular headlamp frame", exterior, V(side * .695f, .782f, 2.343f), V(.366f, .194f, .052f), chrome, .018f);
                for (int lamp = 0; lamp < 2; lamp++)
                {
                    float x = side * (.598f + lamp * .19f);
                    Box("Headlamp dark gasket", exterior, V(x, .782f, 2.376f), V(.174f, .174f, .024f), rubber, .010f);
                    Box("Headlamp ribbed lens", exterior, V(x, .782f, 2.393f), V(.158f, .152f, .024f), headlamp, .011f);
                    for (int rib = 0; rib < 6; rib++)
                        Box("Headlamp glass flute", exterior, V(x - .063f + rib * .025f, .782f, 2.407f), V(.0025f, .135f, .003f), chrome, .001f);
                }
                Box("Front amber signal", exterior, V(side * .729f, .591f, 2.360f), V(.26f, .062f, .025f), amber, .010f);
                Box("Rear lamp rubber gasket", exterior, V(side * .70f, .787f, -2.332f), V(.38f, .194f, .035f), rubber, .015f);
                Box("Rear red lens", exterior, V(side * .638f, .787f, -2.357f), V(.235f, .169f, .022f), red, .008f);
                Box("Rear amber lens", exterior, V(side * .829f, .787f, -2.357f), V(.116f, .169f, .022f), amber, .008f);
                for (int rib = 0; rib < 4; rib++)
                    Box("Rear lens horizontal rib", exterior, V(side * .7f, .73f + rib * .037f, -2.372f), V(.350f, .004f, .004f), steel, .001f);
            }
            for (int end = -1; end <= 1; end += 2)
            {
                float z = end == 1 ? 2.35f : -2.34f;
                Box("Chrome bumper", exterior, V(0, .478f, z), V(1.91f, .151f, .164f), chrome, .045f);
                Box("Rubber bumper impact band", exterior, V(0, .491f, z + end * .088f), V(1.85f, .056f, .020f), rubber, .014f);
                for (int side = -1; side <= 1; side += 2)
                    Box("Bumper rubber corner", exterior, V(side * .884f, .493f, z), V(.15f, .095f, .175f), rubber, .030f);
                Box("License plate mounting", exterior, V(0, .591f, z + end * .041f), V(.327f, .090f, .018f), rubber, .006f);
                Box("Faded license plate", exterior, V(0, .591f, z + end * .054f), V(.296f, .071f, .010f), cream, .004f);
                Label("Ordinary registration", exterior, "JO  1994", V(0, .588f, z + end * .061f), .022f, end == 1 ? Quaternion.Euler(0, 180, 0) : Quaternion.identity, dark.color);
                Disc("Plate screw", exterior, V(-.123f, .61f, z + end * .063f), .004f, .003f, steel, Quaternion.identity);
                Disc("Plate screw", exterior, V(.123f, .61f, z + end * .063f), .004f, .003f, steel, Quaternion.identity);
            }
            Box("Exhaust tail pipe", exterior, V(.59f, .27f, -2.265f), V(.065f, .055f, .26f), steel, .015f);
            Box("Rear trunk keyhole backing", exterior, V(0, .855f, -2.341f), V(.079f, .031f, .018f), chrome, .009f);
        }

        static void Wheels()
        {
            for (int side = -1; side <= 1; side += 2) for (int axle = -1; axle <= 1; axle += 2)
            {
                var wheel = Group((axle == 1 ? "Front" : "Rear") + " road wheel " + side, exterior);
                Vector3 center = V(side * .92f, .34f, axle * 1.55f);
                Quaternion rotation = Quaternion.Euler(0, side * 90, 0);
                MeshObject("Rounded tire", wheel, Torus(.261f, .067f, 36, 10, 1.38f), rubber, center, rotation);
                for (int groove = -1; groove <= 1; groove++)
                    MeshObject("Circumferential tire groove", wheel, Torus(.326f, .0035f, 36, 5), dark, center + V(side * groove * .035f, 0, 0), rotation);
                for (int tread = 0; tread < 28; tread++)
                {
                    float a = tread * Mathf.PI * 2 / 28;
                    Vector3 tangent = V(0, -Mathf.Sin(a), Mathf.Cos(a));
                    Vector3 radial = V(0, Mathf.Cos(a), Mathf.Sin(a));
                    Line("Fine tire tread incision", wheel, new[] { center + radial * .327f + V(-.065f, 0, 0) - tangent * .009f, center + radial * .329f + V(.065f, 0, 0) + tangent * .009f }, .0023f, dark, 4);
                }
                Vector3 face = center + V(side * .090f, 0, 0);
                Disc("Pressed steel rim dish", wheel, face, .212f, .025f, steel, rotation);
                MeshObject("Chrome rim lip", wheel, Torus(.201f, .012f, 32, 7), chrome, face + V(side * .016f, 0, 0), rotation);
                for (int hole = 0; hole < 8; hole++)
                {
                    float a = hole * Mathf.PI * 2 / 8;
                    Vector3 point = face + V(side * .017f, Mathf.Cos(a) * .153f, Mathf.Sin(a) * .153f);
                    var vent = Box("Steel rim oval opening", wheel, point, V(.008f, .048f, .031f), dark, .013f);
                    vent.localRotation = Quaternion.Euler(a * Mathf.Rad2Deg, 0, 0);
                }
                Disc("Plain chrome hubcap", wheel, face + V(side * .026f, 0, 0), .115f, .036f, chrome, rotation);
                MeshObject("Hubcap stamped edge", wheel, Torus(.105f, .0035f, 30, 5), steel, face + V(side * .047f, 0, 0), rotation);
                for (int bolt = 0; bolt < 5; bolt++)
                {
                    float a = bolt * Mathf.PI * 2 / 5;
                    Disc("Wheel nut", wheel, face + V(side * .043f, Mathf.Cos(a) * .075f, Mathf.Sin(a) * .075f), .009f, .010f, steel, rotation, 6);
                }
            }
        }

        static void Cabin()
        {
            Box("Painted shallow roof", exterior, V(0, 1.628f, -.35f), V(1.60f, .052f, 1.53f), paint, .025f);
            Box("Upholstered headliner", interior, V(0, 1.590f, -.36f), V(1.493f, .024f, 1.435f), liner, .011f);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 aBottom = V(side * .854f, .997f, .856f);
                Vector3 aTop = V(side * .737f, 1.603f, .402f);
                Line("Thin painted A pillar", exterior, new[] { aBottom, aTop }, .028f, paint, 8);
                Line("Windshield A pillar seal", exterior, new[] { aBottom + V(-side * .016f, 0, .009f), aTop + V(-side * .016f, 0, .009f) }, .008f, rubber, 6);
                Line("A pillar interior vinyl", interior, new[] { aBottom + V(-side * .012f, 0, -.012f), aTop + V(-side * .012f, -.006f, -.013f) }, .020f, dash, 6);
                Line("B pillar", exterior, new[] { V(side * .873f, .970f, -.52f), V(side * .770f, 1.603f, -.55f) }, .025f, paint, 6);
                Line("B pillar inside", interior, new[] { V(side * .851f, 1.00f, -.52f), V(side * .751f, 1.58f, -.55f) }, .019f, dash, 6);
                Line("C pillar", exterior, new[] { V(side * .902f, .974f, -1.658f), V(side * .770f, 1.607f, -1.108f) }, .046f, paint, 8);
                Line("Rear window seal", exterior, new[] { V(side * .855f, 1.002f, -1.655f), V(side * .726f, 1.586f, -1.108f) }, .009f, rubber, 6);
                Line("Chrome side window sill", exterior, new[] { V(side * .862f, .984f, .839f), V(side * .882f, .984f, -.49f), V(side * .90f, .982f, -1.62f) }, .009f, chrome, 6);
                Line("Roof gutter", exterior, new[] { V(side * .783f, 1.635f, .366f), V(side * .803f, 1.635f, -.95f), V(side * .775f, 1.633f, -1.086f) }, .008f, chrome, 6);
                Box("Interior door lower panel", side == -1 ? driverDoor : interior, V(side * .830f, .712f, -.01f), V(.054f, .486f, 1.32f), dash, .026f);
                Box("Door upholstered insert", side == -1 ? driverDoor : interior, V(side * .797f, .787f, -.015f), V(.018f, .23f, .95f), cloth, .007f);
                DoorTrim(side, .02f, side == -1 ? driverDoor : interior);
                Box("Rear door interior", interior, V(side * .849f, .738f, -1.01f), V(.040f, .428f, .80f), dash, .018f);
                Box("Rear door cloth insert", interior, V(side * .821f, .79f, -1.01f), V(.018f, .218f, .65f), cloth, .006f);
                DoorTrim(side, -.95f, interior);
                var visor = Box("Sun visor", interior, V(side * .407f, 1.552f, .278f), V(.318f, .028f, .147f), liner, .014f);
                visor.localRotation = Quaternion.Euler(8, 0, 0);
                Line("Visor hinge", interior, new[] { V(side * .564f, 1.57f, .318f), V(side * .31f, 1.57f, .318f) }, .005f, steel, 6);
                Box("Belt lower socket", interior, V(side * .765f, .523f, -.73f), V(.05f, .095f, .045f), rubber, .010f);
                Line("Seat belt webbing", interior, new[] { V(side * .768f, .576f, -.73f), V(side * .749f, 1.44f, -.571f) }, .009f, rubber, 4);
            }
            Line("Windshield lower weather seal", exterior, new[] { V(-.84f, 1.006f, .862f), V(0, 1.004f, .890f), V(.84f, 1.006f, .862f) }, .013f, rubber, 6);
            Line("Windshield upper weather seal", exterior, new[] { V(-.731f, 1.60f, .409f), V(.731f, 1.60f, .409f) }, .011f, rubber, 6);
            Line("Rear window lower weather seal", exterior, new[] { V(-.85f, 1.001f, -1.651f), V(.85f, 1.001f, -1.651f) }, .011f, rubber, 6);
            Line("Rear window upper weather seal", exterior, new[] { V(-.731f, 1.59f, -1.10f), V(.731f, 1.59f, -1.10f) }, .011f, rubber, 6);
            Box("Rear parcel shelf", interior, V(0, .994f, -1.541f), V(1.54f, .034f, .28f), dash, .016f);
            Line("Rear view mirror stem", interior, new[] { V(0, 1.591f, .361f), V(0, 1.503f, .487f) }, .006f, steel, 7);
            Box("Rear view mirror housing", interior, V(0, 1.470f, .501f), V(.213f, .078f, .028f), rubber, .014f);
            Box("Rear view mirror face", interior, V(0, 1.470f, .482f), V(.189f, .056f, .006f), glassMirror, .010f);
            Box("Mirror day-night tab", interior, V(.057f, 1.421f, .497f), V(.035f, .013f, .023f), rubber, .005f);
            Box("Dome lamp surround", interior, V(0, 1.568f, -.58f), V(.16f, .024f, .075f), dash, .012f);
            Box("Dome lamp frosted cover", interior, V(0, 1.551f, -.58f), V(.117f, .010f, .048f), cream, .006f);
            // Open windows keep the view and all seated interaction rays unobstructed.
        }

        static void DoorTrim(int side, float z, Transform group)
        {
            Box("Door armrest", group, V(side * .766f, .884f, z - .105f), V(.103f, .071f, .392f), dash, .025f);
            Box("Door pull pocket", group, V(side * .746f, .922f, z - .125f), V(.058f, .010f, .156f), dark, .004f);
            Box("Interior handle recess", group, V(side * .814f, 1.04f, z), V(.017f, .064f, .184f), dark, .008f);
            Line("Interior chrome door handle", group, new[] { V(side * .787f, 1.04f, z - .070f), V(side * .758f, 1.04f, z - .055f), V(side * .758f, 1.04f, z + .073f), V(side * .787f, 1.04f, z + .088f) }, .008f, chrome, 8);
            Disc("Manual window winder hub", group, V(side * .783f, .79f, z + .205f), .024f, .019f, steel, Quaternion.Euler(0, side * 90, 0));
            Line("Manual window crank", group, new[] { V(side * .767f, .79f, z + .205f), V(side * .750f, .752f, z + .245f), V(side * .742f, .741f, z + .263f) }, .007f, chrome, 7);
            Disc("Window crank grip", group, V(side * .730f, .741f, z + .263f), .014f, .026f, rubber, Quaternion.Euler(0, side * 90, 0));
            Box("Door storage pocket", group, V(side * .782f, .573f, z - .175f), V(.066f, .127f, .47f), dash, .017f);
            Box("Door pocket dark opening", group, V(side * .752f, .632f, z - .175f), V(.037f, .008f, .418f), dark, .002f);
            for (int rib = 0; rib < 9; rib++)
                Box("Door speaker grille slit", group, V(side * .790f, .673f, z + .348f - rib * .017f), V(.004f, .091f, .006f), dark, .002f);
        }

        static void Dashboard()
        {
            MeshObject("Shaped dashboard", interior, DashboardMesh(), dash, V(.285f, 0, 0));
            Box("Instrument lower plinth", interior, V(-.43f, .832f, .642f), V(.475f, .098f, .374f), dash, .015f);
            Box("Driver outer vent shoulder", interior, V(-.713f, .911f, .626f), V(.110f, .181f, .355f), dash, .014f);
            Box("Dashboard lower fascia", interior, V(0, .79f, .533f), V(1.54f, .182f, .225f), dash, .022f);
            Line("Dashboard stitched leading edge", interior, new[] { V(-.744f, .997f, .433f), V(-.35f, 1.005f, .448f), V(.20f, .993f, .447f), V(.744f, .98f, .455f) }, .002f, clothSeam, 5);
            Box("Instrument binnacle hood", interior, V(-.43f, .986f, .695f), V(.451f, .056f, .202f), dash, .024f);
            Box("Instrument cluster recess", interior, V(-.43f, .936f, .591f), V(.420f, .139f, .024f), rubber, .012f);
            Box("Instrument cluster matte face", interior, V(-.43f, .936f, .575f), V(.400f, .121f, .005f), dark, .006f);
            Gauge(V(-.528f, .940f, .568f), .056f, true);
            Gauge(V(-.365f, .940f, .568f), .052f, false);
            for (int i = 0; i < 4; i++)
                Box("Cluster warning indicator", interior, V(-.459f + i * .019f, .896f, .565f), V(.010f, .005f, .003f), i == 0 ? red : instrument, .001f);
            Label("Odometer", interior, "084 219", V(-.528f, .918f, .560f), .011f, Quaternion.identity, instrument.color);
            Label("Speedometer unit", interior, "km/h", V(-.528f, .946f, .560f), .009f, Quaternion.identity, instrument.color);
            for (int i = 0; i < 4; i++) Vent(i == 0 ? -.718f : i == 1 ? .045f : i == 2 ? .249f : .647f, .936f, i == 0 ? .089f : .13f);
            Box("Cassette radio recessed surround", interior, V(.079f, .855f, .410f), V(.366f, .117f, .026f), dark, .007f);
            Box("Cassette radio metal face", interior, V(.079f, .855f, .389f), V(.347f, .096f, .015f), steel, .005f);
            Box("Cassette tape slot", interior, V(.074f, .875f, .377f), V(.177f, .020f, .006f), dark, .003f);
            Box("Cassette eject tab", interior, V(.175f, .875f, .371f), V(.021f, .016f, .009f), chrome, .003f);
            Box("Radio amber frequency strip", interior, V(.077f, .836f, .374f), V(.146f, .012f, .005f), amber, .002f);
            for (int side = -1; side <= 1; side += 2)
            {
                Disc("Radio round tuning knob", interior, V(.079f + side * .143f, .854f, .370f), .023f, .014f, rubber, Quaternion.identity, 20);
                MeshObject("Radio knob chrome ring", interior, Torus(.020f, .0025f, 20, 5), chrome, V(.079f + side * .143f, .854f, .360f));
                Box("Radio knob white tick", interior, V(.079f + side * .143f, .867f, .355f), V(.003f, .007f, .003f), cream, .001f);
            }
            for (int i = 0; i < 5; i++) Box("Radio preset button", interior, V(.024f + i * .027f, .808f, .374f), V(.019f, .010f, .012f), rubber, .002f);
            Box("Heater control panel", interior, V(.073f, .741f, .416f), V(.281f, .067f, .020f), dash, .006f);
            for (int row = 0; row < 2; row++)
            {
                Box("Heater slider track", interior, V(.073f, .728f + row * .027f, .401f), V(.189f, .005f, .005f), dark, .001f);
                Box("Heater slider tab", interior, V(row == 0 ? .109f : .024f, .728f + row * .027f, .394f), V(.027f, .017f, .012f), chrome, .003f);
            }
            Box("Glovebox inset front", interior, V(.528f, .800f, .412f), V(.354f, .17f, .025f), dash, .010f);
            Line("Glovebox perimeter seam", interior, new[] { V(.351f, .883f, .396f), V(.701f, .883f, .396f), V(.701f, .716f, .396f), V(.351f, .716f, .396f), V(.351f, .883f, .396f) }, .002f, dark, 5);
            Box("Glovebox latch", interior, V(.532f, .858f, .393f), V(.072f, .022f, .015f), steel, .005f);
            Disc("Glovebox lock", interior, V(.532f, .858f, .382f), .005f, .003f, dark, Quaternion.identity, 12);
            Box("Console lower tunnel", interior, V(.092f, .60f, -.025f), V(.243f, .38f, .905f), dash, .042f);
            Box("Console sloped upper bridge", interior, V(.114f, .849f, .245f), V(.191f, .288f, .272f), dash, .023f);
            Box("Phone resting tray", interior, V(.12f, .995f, .22f), V(.158f, .028f, .224f), dash, .013f);
            Box("Phone tray non-slip inset", interior, V(.12f, 1.009f, .22f), V(.118f, .004f, .196f), rubber, .004f);
            Box("Manual shifter surround", interior, V(.079f, .815f, -.174f), V(.159f, .023f, .169f), steel, .018f);
            Disc("Accordion shifter boot", interior, V(.079f, .847f, -.174f), .063f, .065f, rubber, Quaternion.Euler(90, 0, 0), 20);
            for (int i = 0; i < 4; i++) MeshObject("Shifter boot fold", interior, Torus(.034f + i * .006f, .004f, 20, 5), rubber, V(.079f, .865f - i * .015f, -.174f), Quaternion.Euler(90, 0, 0));
            Line("Manual gear lever", interior, new[] { V(.079f, .867f, -.174f), V(.068f, .936f, -.21f) }, .008f, steel, 8);
            Box("Rounded manual gear knob", interior, V(.068f, .947f, -.214f), V(.039f, .036f, .043f), rubber, .017f);
            Line("Handbrake lever", interior, new[] { V(.10f, .718f, -.435f), V(.105f, .772f, -.285f) }, .013f, rubber, 8);
            Disc("Handbrake end button", interior, V(.105f, .777f, -.271f), .007f, .010f, chrome, Quaternion.Euler(20, 0, 0));
            Box("Console small rear ashtray", interior, V(.091f, .751f, -.516f), V(.15f, .025f, .12f), dash, .011f);
            Steering();
        }

        static void Vent(float x, float y, float width)
        {
            Box("Dashboard vent surround", interior, V(x, y, .429f), V(width, .064f, .021f), steel, .005f);
            Box("Dashboard vent recess", interior, V(x, y, .414f), V(width - .012f, .051f, .005f), dark, .003f);
            for (int i = 0; i < 4; i++) Box("Vent horizontal louvre", interior, V(x, y - .017f + i * .011f, .410f), V(width - .020f, .0035f, .009f), dash, .001f);
            Box("Vent direction tab", interior, V(x, y, .400f), V(.011f, .026f, .007f), steel, .002f);
        }

        static void Gauge(Vector3 center, float radius, bool speed)
        {
            Disc("Gauge dial", interior, center, radius, .002f, dark, Quaternion.identity, 40);
            MeshObject("Thin gauge bezel", interior, Torus(radius + .001f, .0025f, 36, 5), steel, center - V(0, 0, .002f));
            for (int tick = 0; tick <= 20; tick++)
            {
                float a = (220 - tick * 13f) * Mathf.Deg2Rad;
                float length = tick % 5 == 0 ? .008f : .004f;
                Vector3 direction = V(Mathf.Cos(a), Mathf.Sin(a), 0);
                Line("Analog dial graduation", interior, new[] { center + direction * (radius - .007f) - V(0, 0, .005f), center + direction * (radius - .007f - length) - V(0, 0, .005f) }, tick % 5 == 0 ? .0012f : .0008f, instrument, 4);
                if (tick % 5 == 0)
                {
                    string value = speed ? (tick * 8).ToString() : (tick / 5 * 2).ToString();
                    Label("Gauge numeral", interior, value, center + direction * (radius - .020f) - V(0, 0, .006f), .008f, Quaternion.identity, instrument.color);
                }
            }
            float angle = (speed ? 186 : 210) * Mathf.Deg2Rad;
            Vector3 needleEnd = center + V(Mathf.Cos(angle), Mathf.Sin(angle), 0) * (radius - .010f) - V(0, 0, .008f);
            Line("Restrained orange gauge needle", interior, new[] { center - V(0, 0, .008f), needleEnd }, .0014f, needle, 5);
            Disc("Needle pin", interior, center - V(0, 0, .011f), .0045f, .003f, steel, Quaternion.identity, 14);
        }

        static void Steering()
        {
            var wheel = Group("Conventional near-circular steering wheel", interior);
            wheel.localPosition = V(-.43f, .99f, .42f);
            wheel.localRotation = Quaternion.Euler(10, 0, 0);
            MeshObject("Steering padded rim", wheel, Torus(.198f, .017f, 48, 9), rubber);
            MeshObject("Steering rim inner stitching", wheel, Torus(.190f, .0016f, 48, 5), clothSeam, V(0, 0, -.014f));
            for (int side = -1; side <= 1; side += 2)
                Line("Steering horizontal spoke", wheel, new[] { V(side * .050f, -.012f, 0), V(side * .112f, -.018f, 0), V(side * .18f, -.035f, 0) }, .013f, rubber, 7);
            Line("Steering lower spoke", wheel, new[] { V(0, -.043f, 0), V(0, -.112f, 0), V(0, -.18f, 0) }, .015f, rubber, 7);
            Box("Simple padded steering horn", wheel, V(0, -.015f, -.010f), V(.137f, .079f, .044f), rubber, .019f);
            Box("Horn center inset", wheel, V(0, -.015f, -.036f), V(.083f, .031f, .006f), dash, .006f);
            Disc("Steering column shroud", interior, V(-.43f, .897f, .531f), .038f, .22f, dash, Quaternion.Euler(24, 0, 0), 20);
            Line("Turn signal stalk", interior, new[] { V(-.473f, .938f, .478f), V(-.593f, .936f, .465f) }, .006f, rubber, 7);
            Box("Turn signal stalk paddle", interior, V(-.601f, .936f, .465f), V(.036f, .022f, .025f), rubber, .006f);
            Line("Wiper stalk", interior, new[] { V(-.39f, .936f, .488f), V(-.295f, .927f, .475f) }, .006f, rubber, 7);
        }

        static void Seats()
        {
            Box("Cabin floor carpet", interior, V(0, .463f, -.39f), V(1.51f, .027f, 2.39f), clothSeam, .012f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * .43f;
                float seatY = side == -1 ? .634f : .720f;
                Box("Front seat base", interior, V(x, seatY - .112f, -.372f), V(.616f, .16f, .811f), rubber, .042f);
                Box("Rounded upholstered front cushion", interior, V(x, seatY, -.349f), V(.601f, .151f, .829f), cloth, .063f);
                Box("Seat cushion central cloth panel", interior, V(x, seatY + .070f, -.342f), V(.361f, .024f, .655f), cloth, .011f);
                for (int channel = -2; channel <= 2; channel++)
                    Line("Cushion stitched longitudinal channel", interior, new[] { V(x + channel * .068f, seatY + .083f, -.646f), V(x + channel * .068f, seatY + .087f, -.340f), V(x + channel * .068f, seatY + .082f, -.041f) }, .0017f, clothSeam, 5);
                Line("Front cushion piping", interior, RoundedRectangle(x, seatY + .040f, -.349f, .544f, .759f, .06f), .003f, clothSeam, 6);
                var back = Box("Upholstered front seatback", interior, V(x, .962f, -.805f), V(.59f, .615f, .151f), cloth, .048f);
                back.localRotation = Quaternion.Euler(-9, 0, 0);
                Box("Front backrest inset", interior, V(x, .996f, -.712f), V(.377f, .43f, .021f), cloth, .010f);
                for (int channel = -2; channel <= 2; channel++)
                    Line("Backrest seam", interior, new[] { V(x + channel * .069f, .798f, -.705f), V(x + channel * .069f, 1.183f, -.733f) }, .0018f, clothSeam, 5);
                for (int pin = -1; pin <= 1; pin += 2)
                    Line("Headrest steel post", interior, new[] { V(x + pin * .086f, 1.239f, -.856f), V(x + pin * .086f, 1.337f, -.856f) }, .007f, steel, 7);
                Box("Rounded front headrest", interior, V(x, 1.346f, -.855f), V(.331f, .17f, .146f), cloth, .046f);
                Line("Headrest horizontal seam", interior, new[] { V(x - .128f, 1.346f, -.779f), V(x + .128f, 1.346f, -.779f) }, .002f, clothSeam, 5);
                Box("Seatbelt buckle", interior, V(side * .123f, .711f, -.431f), V(.041f, .066f, .048f), rubber, .008f);
                Box("Seatbelt release button", interior, V(side * .123f, .747f, -.431f), V(.026f, .008f, .032f), red, .002f);
                Box("Front ribbed floor mat", interior, V(x, .482f, .183f), V(.42f, .008f, .481f), rubber, .013f);
                for (int i = 0; i < 11; i++)
                    Box("Floor mat fine rib", interior, V(x, .488f, -.018f + i * .039f), V(.374f, .003f, .003f), dash, .001f);
            }
            Box("Rear upholstered bench cushion", interior, V(0, .663f, -1.196f), V(1.489f, .18f, .507f), cloth, .052f);
            Box("Rear upholstered bench back", interior, V(0, .958f, -1.492f), V(1.47f, .50f, .13f), cloth, .042f);
            for (int channel = -8; channel <= 8; channel++)
            {
                Line("Rear bench back vertical seam", interior, new[] { V(channel * .071f, .754f, -1.422f), V(channel * .071f, 1.15f, -1.422f) }, .0017f, clothSeam, 5);
                Line("Rear bench cushion seam", interior, new[] { V(channel * .071f, .758f, -1.402f), V(channel * .071f, .758f, -.983f) }, .0017f, clothSeam, 5);
            }
            for (int side = -1; side <= 1; side += 2)
                Box("Rear bench low headrest", interior, V(side * .46f, 1.187f, -1.481f), V(.303f, .14f, .13f), cloth, .040f);
        }

        static void Props()
        {
            // The closed case provides the actual support directly below the flashlight anchor.
            Box("Closed detective equipment case", interior, V(.40f, .903f, -.025f), V(.52f, .20f, .40f), leather, .025f);
            Line("Case lid seam", interior, RoundedRectangle(.40f, .947f, -.025f, .511f, .391f, .025f), .003f, dark, 5);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Case brass latch", interior, V(.40f + side * .174f, .957f, -.231f), V(.045f, .041f, .012f), steel, .005f);
                Box("Case corner protector", interior, V(.40f + side * .225f, .952f, -.19f), V(.059f, .083f, .049f), steel, .009f);
            }
            Line("Case carry handle", interior, new[] { V(.331f, .873f, -.23f), V(.331f, .841f, -.252f), V(.469f, .841f, -.252f), V(.469f, .873f, -.23f) }, .007f, leather, 7);
            Box("Detective pocket notebook", interior, V(.553f, .813f, -.379f), V(.16f, .016f, .221f), leather, .005f);
            Box("Notebook aged page block", interior, V(.553f, .815f, -.379f), V(.151f, .011f, .212f), paper, .002f);
            Box("Notebook closed top cover", interior, V(.553f, .823f, -.379f), V(.163f, .004f, .225f), leather, .002f);
            Line("Notebook elastic closure", interior, new[] { V(.601f, .827f, -.49f), V(.601f, .827f, -.27f) }, .002f, rubber, 5);
            Box("Folded paper road map", interior, V(.417f, .804f, -.55f), V(.225f, .009f, .176f), paper, .003f);
            for (int i = 0; i < 3; i++)
                Line("Map crease", interior, new[] { V(.334f + i * .071f, .811f, -.630f), V(.334f + i * .071f, .811f, -.471f) }, .0009f, cream, 4);
            Line("Map route", interior, new[] { V(.323f, .813f, -.598f), V(.365f, .813f, -.559f), V(.411f, .813f, -.571f), V(.454f, .813f, -.526f), V(.510f, .813f, -.513f) }, .0013f, red, 4);
            Line("Map river", interior, new[] { V(.359f, .813f, -.632f), V(.371f, .813f, -.602f), V(.432f, .813f, -.581f), V(.469f, .813f, -.497f) }, .0015f, steel, 4);
        }

        static Mesh Hood()
        {
            return Loft(new[] { .84f, 1.74f, 2.285f }, new[] { .848f, .878f, .844f }, new[] { .867f, .842f, .817f }, new[] { .954f, .936f, .905f }, .025f);
        }

        static Mesh DashboardMesh()
        {
            return Loft(new[] { .44f, .68f, .84f }, new[] { .475f, .485f, .485f }, new[] { .79f, .79f, .82f }, new[] { .98f, 1.01f, .99f }, .018f);
        }

        static Mesh BodySide(int side)
        {
            var p = new List<Vector2> { new Vector2(-2.30f, .40f) };
            p.Add(new Vector2(-1.937f, .40f));
            for (int axle = -1; axle <= 1; axle += 2)
            {
                for (int i = 0; i <= 20; i++)
                {
                    float a = Mathf.PI - i * Mathf.PI / 20;
                    p.Add(new Vector2(axle * 1.55f + Mathf.Cos(a) * .375f, .34f + Mathf.Sin(a) * .375f));
                }
            }
            p.Add(new Vector2(2.30f, .40f));
            p.Add(new Vector2(2.30f, .86f));
            p.Add(new Vector2(2.08f, .922f));
            p.Add(new Vector2(.84f, .975f));
            p.Add(new Vector2(-1.65f, .973f));
            p.Add(new Vector2(-2.15f, .924f));
            p.Add(new Vector2(-2.30f, .879f));
            return ExtrudeYZ(p, side * .922f, .062f);
        }

        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        static Transform Box(string name, Transform parent, Vector3 position, Vector3 size, Material mat, float bevel = .008f)
        {
            float smallest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            var mesh = smallest < .006f ? PlainBox(size) : BevelBox(size, Mathf.Min(bevel, smallest * .48f));
            return MeshObject(name, parent, mesh, mat, position);
        }

        static Transform MeshObject(string name, Transform parent, Mesh mesh, Material mat, Vector3 position = default, Quaternion rotation = default)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation.Equals(default(Quaternion)) ? Quaternion.identity : rotation;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        static void Disc(string name, Transform parent, Vector3 position, float radius, float depth, Material mat, Quaternion rotation, int sides = 24)
        {
            MeshObject(name, parent, Lathe(new[] { new Vector2(0, -depth * .5f), new Vector2(radius * .91f, -depth * .5f), new Vector2(radius, -depth * .30f), new Vector2(radius, depth * .30f), new Vector2(radius * .91f, depth * .5f), new Vector2(0, depth * .5f) }, sides), mat, position, rotation);
        }

        static void Line(string name, Transform parent, Vector3[] path, float radius, Material mat, int sides = 6)
        {
            MeshObject(name, parent, Tube(path, radius, sides), mat);
        }

        static void Label(string name, Transform parent, string text, Vector3 position, float height, Quaternion rotation, Color color)
        {
            // Solid strokes have predictable metric size and obey the dashboard's depth.
            // Dynamic GUI font materials can render through the car and use oversized glyphs.
            var group = Group(name, parent);
            group.localPosition = position;
            group.localRotation = rotation;
            var mat = color.r > .3f ? instrument : dark;
            float advance = height * .68f;
            int[] digitMasks = { 0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f };
            Vector2[][] segments = {
                new[]{new Vector2(-.23f,.5f),new Vector2(.23f,.5f)},
                new[]{new Vector2(.26f,.45f),new Vector2(.26f,.03f)},
                new[]{new Vector2(.26f,-.03f),new Vector2(.26f,-.45f)},
                new[]{new Vector2(-.23f,-.5f),new Vector2(.23f,-.5f)},
                new[]{new Vector2(-.26f,-.45f),new Vector2(-.26f,-.03f)},
                new[]{new Vector2(-.26f,.03f),new Vector2(-.26f,.45f)},
                new[]{new Vector2(-.23f,0),new Vector2(.23f,0)}
            };
            for (int index=0; index<text.Length; index++)
            {
                char c=text[index];
                var strokes=new List<Vector2[]>();
                if(char.IsDigit(c) || c=='O')
                {
                    int mask=digitMasks[c=='O'?0:c-'0'];
                    for(int segment=0;segment<7;segment++)if((mask&(1<<segment))!=0)strokes.Add(segments[segment]);
                }
                else if(c=='J') strokes.Add(new[]{new Vector2(.25f,.5f),new Vector2(.25f,-.3f),new Vector2(.12f,-.5f),new Vector2(-.2f,-.5f),new Vector2(-.25f,-.3f)});
                else if(c=='k')
                {
                    strokes.Add(new[]{new Vector2(-.25f,-.5f),new Vector2(-.25f,.5f)});
                    strokes.Add(new[]{new Vector2(.25f,.4f),new Vector2(-.25f,0),new Vector2(.25f,-.5f)});
                }
                else if(c=='m')strokes.Add(new[]{new Vector2(-.25f,-.5f),new Vector2(-.25f,.35f),new Vector2(0,0),new Vector2(.25f,.35f),new Vector2(.25f,-.5f)});
                else if(c=='h')
                {
                    strokes.Add(new[]{new Vector2(-.25f,-.5f),new Vector2(-.25f,.5f)});
                    strokes.Add(new[]{new Vector2(-.25f,0),new Vector2(.25f,0),new Vector2(.25f,-.5f)});
                }
                else if(c=='/')strokes.Add(new[]{new Vector2(-.22f,-.5f),new Vector2(.22f,.5f)});
                float x=(index-(text.Length-1)*.5f)*advance;
                foreach(var stroke in strokes)
                    Line("Painted instrument stroke",group,stroke.Select(p=>V(x+p.x*height,p.y*height,0)).ToArray(),height*.022f,mat,4);
            }
        }

        static Mesh PlainBox(Vector3 size)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            Vector3 half=size*.5f;
            for(int face=0;face<6;face++)
            {
                int axis=face/2;float sign=face%2==0?1:-1;
                Vector3 n=axis==0?Vector3.right*sign:axis==1?Vector3.up*sign:Vector3.forward*sign;
                Vector3 u=axis==0?Vector3.forward:Vector3.right,v=Vector3.Cross(n,u);
                float hu=Mathf.Abs(Vector3.Dot(half,u)),hv=Mathf.Abs(Vector3.Dot(half,v)),hn=Mathf.Abs(Vector3.Dot(half,n));
                int start=vertices.Count;
                vertices.Add(n*hn-u*hu-v*hv);vertices.Add(n*hn+u*hu-v*hv);
                vertices.Add(n*hn+u*hu+v*hv);vertices.Add(n*hn-u*hu+v*hv);
                for(int i=0;i<4;i++)normals.Add(n);
                uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});
                triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
            return NewMesh(vertices,triangles,normals,uv);
        }

        static Vector3[] RoundedRectangle(float x, float y, float z, float width, float depth, float radius)
        {
            var points = new List<Vector3>();
            for (int corner = 0; corner < 4; corner++) for (int i = 0; i < 6; i++)
            {
                float a = (corner * 90 + i * 90f / 5) * Mathf.Deg2Rad;
                float cx = corner == 0 || corner == 3 ? width / 2 - radius : -width / 2 + radius;
                float cz = corner < 2 ? depth / 2 - radius : -depth / 2 + radius;
                points.Add(V(x + cx + Mathf.Cos(a) * radius, y, z + cz + Mathf.Sin(a) * radius));
            }
            points.Add(points[0]);
            return points.ToArray();
        }

        static Mesh BevelBox(Vector3 size, float radius)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
            Vector3 half = size * .5f, inner = half - Vector3.one * radius;
            for (int face = 0; face < 6; face++)
            {
                int axis = face / 2; float sign = face % 2 == 0 ? 1 : -1;
                Vector3 n = axis == 0 ? Vector3.right * sign : axis == 1 ? Vector3.up * sign : Vector3.forward * sign;
                Vector3 u = axis == 0 ? Vector3.forward : Vector3.right;
                Vector3 v = Vector3.Cross(n, u);
                float hu = Mathf.Abs(Vector3.Dot(half, u)), hv = Mathf.Abs(Vector3.Dot(half, v));
                float[] us = { -hu, -hu + radius, hu - radius, hu };
                float[] vs = { -hv, -hv + radius, hv - radius, hv };
                int offset = vertices.Count;
                for (int j = 0; j < 4; j++) for (int i = 0; i < 4; i++)
                {
                    Vector3 p = n * Mathf.Abs(Vector3.Dot(half, n)) + u * us[i] + v * vs[j];
                    Vector3 clamped = V(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                    Vector3 normal = (p - clamped).normalized;
                    vertices.Add(clamped + normal * radius); normals.Add(normal);
                    uv.Add(new Vector2(i / 3f, j / 3f));
                }
                for (int j = 0; j < 3; j++) for (int i = 0; i < 3; i++)
                {
                    int a = offset + j * 4 + i, b = a + 1, c = a + 4, d = c + 1;
                    tris.AddRange(new[] { a, b, d, a, d, c });
                }
            }
            return NewMesh(vertices, tris, normals, uv);
        }

        static Mesh Tube(Vector3[] path, float radius, int sides)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 direction = i == 0 ? path[1] - path[0] : i == path.Length - 1 ? path[i] - path[i - 1] : path[i + 1] - path[i - 1];
                direction.Normalize();
                Vector3 basis = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > .95f ? Vector3.forward : Vector3.up;
                Vector3 u = Vector3.Cross(direction, basis).normalized, v = Vector3.Cross(direction, u).normalized;
                for (int j = 0; j < sides; j++)
                {
                    float a = j * Mathf.PI * 2 / sides;
                    Vector3 normal = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                    vertices.Add(path[i] + normal * radius); normals.Add(normal); uv.Add(new Vector2(j / (float)sides, i / (float)(path.Length - 1)));
                    if (i == path.Length - 1) continue;
                    int p = i * sides + j, s = i * sides + (j + 1) % sides, q = (i + 1) * sides + j, r = (i + 1) * sides + (j + 1) % sides;
                    tris.AddRange(new[] { p, s, q, s, r, q });
                }
            }
            return NewMesh(vertices, tris, normals, uv);
        }

        static Mesh Torus(float radius, float tube, int segments, int sides, float depthScale = 1)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
            for (int i = 0; i < segments; i++) for (int j = 0; j < sides; j++)
            {
                float a = i * Mathf.PI * 2 / segments, b = j * Mathf.PI * 2 / sides;
                float r = radius + Mathf.Cos(b) * tube;
                vertices.Add(V(Mathf.Cos(a) * r, Mathf.Sin(a) * r, Mathf.Sin(b) * tube * depthScale));
                normals.Add(V(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(a) * Mathf.Cos(b), Mathf.Sin(b) / depthScale).normalized);
                uv.Add(new Vector2(i / (float)segments, j / (float)sides));
                int p = i * sides + j, q = ((i + 1) % segments) * sides + j, rIndex = ((i + 1) % segments) * sides + (j + 1) % sides, s = i * sides + (j + 1) % sides;
                tris.AddRange(new[] { p, q, rIndex, p, rIndex, s });
            }
            return NewMesh(vertices, tris, normals, uv);
        }

        static Mesh Lathe(Vector2[] profile, int sides)
        {
            var vertices = new List<Vector3>(); var tris = new List<int>();
            for (int k = 0; k < profile.Length; k++) for (int j = 0; j < sides; j++)
            {
                float a = j * Mathf.PI * 2 / sides;
                vertices.Add(V(Mathf.Cos(a) * profile[k].x, Mathf.Sin(a) * profile[k].x, profile[k].y));
                if (k == profile.Length - 1) continue;
                int p = k * sides + j, q = k * sides + (j + 1) % sides, r = (k + 1) * sides + j, s = (k + 1) * sides + (j + 1) % sides;
                if (profile[k].x > 0) tris.AddRange(new[] { p, q, s });
                if (profile[k + 1].x > 0) tris.AddRange(new[] { p, s, r });
            }
            return NewMesh(vertices, tris);
        }

        static Mesh Loft(float[] zs, float[] widths, float[] bottoms, float[] tops, float bevel)
        {
            var vertices = new List<Vector3>(); var tris = new List<int>();
            for (int k = 0; k < zs.Length; k++)
            {
                float x = widths[k], lo = bottoms[k], hi = tops[k], b = Mathf.Min(bevel, (hi - lo) * .45f);
                vertices.AddRange(new[] { V(-x + b, lo, zs[k]), V(x - b, lo, zs[k]), V(x, lo + b, zs[k]), V(x, hi - b, zs[k]), V(x - b, hi, zs[k]), V(-x + b, hi, zs[k]), V(-x, hi - b, zs[k]), V(-x, lo + b, zs[k]) });
                if (k == zs.Length - 1) continue;
                for (int j = 0; j < 8; j++)
                {
                    int p = k * 8 + j, q = k * 8 + (j + 1) % 8, r = (k + 1) * 8 + j, s = (k + 1) * 8 + (j + 1) % 8;
                    tris.AddRange(new[] { p, q, s, p, s, r });
                }
            }
            for (int j = 1; j < 7; j++)
            {
                tris.AddRange(new[] { 0, j + 1, j });
                int last = (zs.Length - 1) * 8;
                tris.AddRange(new[] { last, last + j, last + j + 1 });
            }
            return NewMesh(vertices, tris);
        }

        static Mesh ExtrudeYZ(List<Vector2> polygon, float x, float depth)
        {
            var vertices = new List<Vector3>(); var tris = new List<int>(); int n = polygon.Count;
            foreach (var p in polygon) vertices.Add(V(x - depth * .5f, p.y, p.x));
            foreach (var p in polygon) vertices.Add(V(x + depth * .5f, p.y, p.x));
            var cap = Triangulate(polygon);
            for (int i = 0; i < cap.Count; i += 3)
            {
                tris.AddRange(new[] { cap[i], cap[i + 1], cap[i + 2] });
                tris.AddRange(new[] { n + cap[i + 2], n + cap[i + 1], n + cap[i] });
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                tris.AddRange(new[] { i, n + i, n + j, i, n + j, j });
            }
            return NewMesh(vertices, tris);
        }

        static List<int> Triangulate(List<Vector2> polygon)
        {
            var remaining = Enumerable.Range(0, polygon.Count).ToList(); var result = new List<int>();
            float area = 0;
            for (int i = 0; i < polygon.Count; i++) area += Cross(polygon[i], polygon[(i + 1) % polygon.Count]);
            if (area < 0) remaining.Reverse();
            int guard = polygon.Count * polygon.Count;
            while (remaining.Count > 2 && guard-- > 0)
            {
                bool cut = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    if (Cross(polygon[b] - polygon[a], polygon[c] - polygon[b]) < .000001f) continue;
                    bool occupied = remaining.Any(p => p != a && p != b && p != c && InTriangle(polygon[p], polygon[a], polygon[b], polygon[c]));
                    if (occupied) continue;
                    result.AddRange(new[] { a, b, c }); remaining.RemoveAt(i); cut = true; break;
                }
                if (!cut) break;
            }
            if (remaining.Count > 2) throw new InvalidOperationException("Sedan body polygon could not be triangulated.");
            return result;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            return Cross(b - a, p - a) >= -0.000001f && Cross(c - b, p - b) >= -0.000001f && Cross(a - c, p - c) >= -0.000001f;
        }

        static Mesh NewMesh(List<Vector3> vertices, List<int> tris, List<Vector3> normals = null, List<Vector2> uv = null)
        {
            var mesh = new Mesh { name = "Native sedan detail", indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetTriangles(tris, 0);
            if (normals != null) mesh.SetNormals(normals); else mesh.RecalculateNormals();
            if (uv != null) mesh.SetUVs(0, uv);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Merge(Transform group, string key)
        {
            var filters = group.GetComponentsInChildren<MeshFilter>(true);
            var sets = filters.GroupBy(f => f.GetComponent<MeshRenderer>().sharedMaterial).ToArray();
            var transient = filters.Select(f => f.sharedMesh).Distinct().ToArray();
            foreach (var set in sets)
            {
                var mesh = new Mesh { name = "Detective sedan " + key + " " + set.Key.name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(set.Select(f => new CombineInstance { mesh = f.sharedMesh, transform = group.worldToLocalMatrix * f.transform.localToWorldMatrix }).ToArray(), true, true);
                mesh.RecalculateBounds();
                string path = Assets + "/" + key + "_" + set.Key.name + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved) { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); }
                else { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
                EditorUtility.SetDirty(saved);
                MeshObject(set.Key.name, group, saved, set.Key);
            }
            foreach (var filter in filters) UnityEngine.Object.DestroyImmediate(filter.gameObject);
            foreach (var mesh in transient) if (mesh && !AssetDatabase.Contains(mesh)) UnityEngine.Object.DestroyImmediate(mesh);
            foreach (var t in group.GetComponentsInChildren<Transform>(true).Reverse())
                if (t != group && t.childCount == 0 && t.GetComponents<Component>().Length == 1) UnityEngine.Object.DestroyImmediate(t.gameObject);
        }

        static void Folders(string path)
        {
            string current = "Assets";
            foreach (var part in path.Split('/').Skip(1))
            {
                if (!AssetDatabase.IsValidFolder(current + "/" + part)) AssetDatabase.CreateFolder(current, part);
                current += "/" + part;
            }
        }
    }
}
