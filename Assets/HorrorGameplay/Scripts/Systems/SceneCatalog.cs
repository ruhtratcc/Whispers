// Aponta para as cenas e o livro da campanha em uso.

using UnityEngine;

namespace HorrorGame
{
    [CreateAssetMenu(menuName = "Whispers/Scene Catalog")]
    public sealed class SceneCatalog : ScriptableObject
    {
        [Header("Scene names — names only, without .unity")]
        [Tooltip("Must be enabled in Build Profiles / Scene List.")]
        public string mainMenuScene = "MainMenu";
        [Tooltip("Replace TestScene with your map when it is ready.")]
        public string gameplayScene = "TestScene";
        [Header("Narrative — future maps stay empty until they exist")]
        public StoryBook storyBook;
    }
}
