using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>Вешает на камеру гашение ветра при отдалении.</summary>
    public static class WindKit
    {
        private const string Arena = "Assets/_Game/Scenes/ArenaAuthor.unity";

        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().path != Arena)
                EditorSceneManager.OpenScene(Arena, OpenSceneMode.Single);

            int added = 0;

            foreach (var cam in Object.FindObjectsByType<Camera>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cam == null || cam.GetComponent<IsoRPG.Cameras.IsoCameraRig>() == null) continue;
                if (cam.GetComponent<IsoRPG.World.CalmDistantWind>() != null) continue;

                cam.gameObject.AddComponent<IsoRPG.World.CalmDistantWind>();
                added++;
            }

            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[IsoRPG] Гашение ветра вдали: добавлено камерам " + added + ".");
        }
    }
}
