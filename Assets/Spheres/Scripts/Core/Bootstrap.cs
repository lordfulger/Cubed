using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// Builds the whole game from code when any scene loads, so the project needs no prefabs or scene setup.
    /// </summary>
    public static class Bootstrap
    {
        static T[] FindAll<T>() where T : Object =>
#if UNITY_2023_1_OR_NEWER
            Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            Object.FindObjectsOfType<T>();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (GameManager.Instance != null) return;
            Application.targetFrameRate = 144;

            // Silence stray scene objects (e.g. a default Main Camera) so they don't fight our cameras/listener.
            foreach (var cam in FindAll<Camera>()) cam.enabled = false;
            foreach (var listener in FindAll<AudioListener>()) listener.enabled = false;
            foreach (var light in FindAll<Light>())
                if (light.type == LightType.Directional) light.enabled = false;

            var go = new GameObject("Spheres");
            go.AddComponent<GameManager>();
        }
    }
}
