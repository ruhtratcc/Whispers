// Ferramenta de preparação ou verificação usada no Editor.

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace HorrorGame.Editor
{
    public sealed class WhispersReleaseBuildValidation : BuildPlayerProcessor
    {
        public const string ProfilePath = "Assets/Settings/Build Profiles/Whispers Windows Prototype.asset";
        public static readonly string[] Scenes = {
            "Assets/HorrorMenu/Scenes/MainMenu.unity",
            "Assets/ChapterOneOffice/Scenes/Chapter1Office.unity",
            "Assets/RoadChapter/Scenes/RoadJourney.unity",
            "Assets/SchoolCEEP/Scenes/SchoolCEEP.unity"
        };
        public override int callbackOrder => 0;
        public override void PrepareForBuild(BuildPlayerContext context)
        {
            var options = context.BuildPlayerOptions;
            if (options.target != BuildTarget.StandaloneWindows64
                || (options.options & (BuildOptions.Development | BuildOptions.IncludeTestAssemblies)) != 0) return;
            if (options.scenes == null || !options.scenes.SequenceEqual(Scenes, StringComparer.Ordinal))
                throw new BuildFailedException("A distribuição de Whispers deve usar as quatro cenas do perfil Windows, na ordem MainMenu, escritório, estrada, escola.");
        }
    }
}
