// Verifica .

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HorrorGame.Tests
{
    [Category("ModelReplacementTools")]
    public sealed class ModelReplacementToolsTests
    {
        string temporary, assetFolder;
        Type tool;

        [SetUp] public void SetUp()
        {
            // School's Editor tools live in Assembly-CSharp-Editor, outside this asmdef's references.
            tool = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("SchoolCEEP.Editor.ModelReplacementTools"))
                .FirstOrDefault(t => t != null);
            Assert.That(tool, Is.Not.Null, "Compile the Editor tool before running this suite.");
            temporary = Path.Combine(Path.GetTempPath(), "Whispers-ModelTools-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            assetFolder = "Assets/__ModelToolsTest_" + Guid.NewGuid().ToString("N");
        }

        object Pack(string arm, string output, string normal = null, bool dx = false)
            => tool.GetMethod("PackArm", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { arm, output, normal, dx, 1024 });

        static void Save(string path, params Color32[] pixels)
        {
            var t = new Texture2D(pixels.Length, 1, TextureFormat.RGBA32, false, true);
            try { t.SetPixels32(pixels); t.Apply(); File.WriteAllBytes(path, t.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(t); }
        }
        static Color32[] Read(string asset)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try { Assert.That(t.LoadImage(File.ReadAllBytes(asset)), Is.True); return t.GetPixels32(); }
            finally { UnityEngine.Object.DestroyImmediate(t); }
        }
        static string Field(object result, string field) => (string)result.GetType().GetField(field).GetValue(result);

        [Test] public void ArmPackingUsesMetalInRedAndInvertedRoughnessInAlphaWithoutChangingSources()
        {
            string arm = Path.Combine(temporary, "source.png");
            Save(arm, new Color32(17, 43, 211, 5), new Color32(251, 255, 0, 201));
            byte[] before = File.ReadAllBytes(arm);
            var maps = Pack(arm, assetFolder);
            var metallic = Read(Field(maps, "metallicSmoothness"));
            var ao = Read(Field(maps, "occlusion"));
            Assert.That(metallic[0], Is.EqualTo(new Color32(211, 0, 0, 212)));
            Assert.That(metallic[1], Is.EqualTo(new Color32(0, 0, 0, 0)));
            Assert.That(ao[0], Is.EqualTo(new Color32(17, 17, 17, 255)));
            Assert.That(ao[1], Is.EqualTo(new Color32(251, 251, 251, 255)));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(arm));
            var importer = (TextureImporter)AssetImporter.GetAtPath(Field(maps, "metallicSmoothness"));
            Assert.That(importer.sRGBTexture, Is.False);
            Assert.That(importer.alphaIsTransparency, Is.False);
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.maxTextureSize, Is.EqualTo(1024));
        }

        [Test] public void DirectXNormalInvertsOnlyGreenAndLeavesOriginalBytesUntouched()
        {
            string arm = Path.Combine(temporary, "arm.png"), normal = Path.Combine(temporary, "normal.png");
            Save(arm, new Color32(255, 128, 0, 255));
            Save(normal, new Color32(101, 23, 239, 171));
            byte[] before = File.ReadAllBytes(normal);
            var maps = Pack(arm, assetFolder, normal, true);
            Assert.That(Read(Field(maps, "normal"))[0], Is.EqualTo(new Color32(101, 232, 239, 171)));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(normal));
            Assert.That(((TextureImporter)AssetImporter.GetAtPath(Field(maps, "normal"))).textureType, Is.EqualTo(TextureImporterType.NormalMap));
            var glMaps = Pack(arm, assetFolder + "/OpenGL", normal, false);
            Assert.That(Read(Field(glMaps, "normal"))[0], Is.EqualTo(new Color32(101, 23, 239, 171)), "OpenGL normals must not be inverted again.");
        }

        [TestCase("Assets/../outside")]
        [TestCase("C:/outside")]
        [TestCase("Assets/./folder")]
        public void TraversalOrAbsoluteOutputIsRejectedBeforeAnythingIsWritten(string bad)
        {
            string arm = Path.Combine(temporary, "arm.png"); Save(arm, new Color32(1, 2, 3, 255));
            var ex = Assert.Throws<TargetInvocationException>(() => Pack(arm, bad));
            Assert.That(ex.InnerException, Is.InstanceOf<ArgumentException>());
            Assert.That(Directory.Exists(assetFolder), Is.False);
        }

        [Test] public void ExistingDerivativeAndIncompleteInputsArePreserved()
        {
            string arm = Path.Combine(temporary, "arm.png"); Save(arm, new Color32(1, 2, 3, 255));
            var ex = Assert.Throws<TargetInvocationException>(() => Pack(arm, assetFolder, null, true));
            Assert.That(ex.InnerException, Is.InstanceOf<ArgumentException>());
            Assert.That(Directory.Exists(assetFolder), Is.False);
            var maps = Pack(arm, assetFolder); string target = Field(maps, "metallicSmoothness");
            byte[] before = File.ReadAllBytes(target);
            ex = Assert.Throws<TargetInvocationException>(() => Pack(arm, assetFolder));
            Assert.That(ex.InnerException, Is.InstanceOf<IOException>());
            CollectionAssert.AreEqual(before, File.ReadAllBytes(target));
        }

        [TearDown] public void TearDown()
        {
            if (!string.IsNullOrEmpty(assetFolder)) AssetDatabase.DeleteAsset(assetFolder);
            if (!string.IsNullOrEmpty(temporary) && Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }
}
#endif
