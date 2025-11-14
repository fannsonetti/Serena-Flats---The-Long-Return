using System.Collections;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

#if IL2CPP
using Il2CppInterop.Runtime.Injection;
#endif

public static class OverpassModule
{
    private const string RootName = "OverpassColliders";
    private static int _spawnCounter = 0;

    // MelonPreferences: debug visibility toggle
    private static MelonPreferences_Category overpassCategory;
    private static MelonPreferences_Entry<bool> debugEnabled;

    // Reference paths in the live scene (used for transform context / cleanup)
    private const string OverpassRootPath = "Map/Hyland Point/Overpass";
    private const string OverpassRampParentForCollider = "Map/Hyland Point/Overpass/Overpass Ramp/overpass_ramp";

    private static readonly string[] RoadblockerTargets =
    {
        "Map/Hyland Point/Overpass/Overpass Ramp/RoadBlocker4_LOD (1)",
        "Map/Hyland Point/Overpass/Overpass Ramp/RoadBlocker4_LOD (2)",
        "Map/Hyland Point/Overpass/Overpass Ramp/RoadBlocker4_LOD (3)",
        "Map/Hyland Point/Overpass/Overpass Ramp/RoadBlocker4_LOD (4)"
    };

    private static readonly Color[] BrightPalette =
    {
        new Color(0.2f, 1f, 0.3f, 1f),
        new Color(0.2f, 0.4f, 1f, 1f),
        new Color(1f, 0.2f, 0.2f, 1f),
        new Color(1f, 0f, 1f, 1f),
        new Color(0f, 1f, 1f, 1f)
    };
    private static int _colorIndex = 0;
    private static Color NextColor()
    {
        var c = BrightPalette[_colorIndex];
        _colorIndex = (_colorIndex + 1) % BrightPalette.Length;
        return c;
    }

    private static bool built = false;

    // ---- Lifecycle (called from Main.cs) ----
    public static void Initialize()
    {
        // Preferences: create category & entry (default false = invisible)
        overpassCategory = MelonPreferences.CreateCategory("OverpassModule");
        debugEnabled = overpassCategory.CreateEntry("Debug", false, "Enable debug rendering (show RC/trigger cubes)");

#if IL2CPP
        ClassInjector.RegisterTypeInIl2Cpp<TeleportOnTrigger>();
#endif
        MelonLogger.Msg("[Overpass] Module initialized. Debug rendering = " + debugEnabled.Value);
    }

    public static void OnSceneLoaded(string sceneName)
    {
        if (sceneName != "Main" || built) return;
        built = true;
        MelonCoroutines.Start(RunAfterDelay(1f));
    }

    public static void OnUpdate()
    {
        // F2: spawn visible test cube under OverpassColliders
        if (Input.GetKeyDown(KeyCode.F2)) SpawnTestCube();

        // Optional F3/F4 left in for convenience
        if (Input.GetKeyDown(KeyCode.F3))
        {
            var tc = Object.FindObjectOfType<TerrainCollider>();
            if (tc) { tc.enabled = !tc.enabled; MelonLogger.Msg($"[Overpass] TerrainCollider enabled = {tc.enabled}"); }
            else MelonLogger.Warning("[Overpass] No TerrainCollider found.");
        }
        if (Input.GetKeyDown(KeyCode.F4))
        {
            var tc = Object.FindObjectOfType<TerrainCollider>();
            MelonLogger.Msg($"[Overpass] TerrainCollider present={tc != null}, enabled={tc?.enabled}");
        }
    }

    // ---- Main build ----
    private static IEnumerator RunAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        var root = EnsureRootIdentity();

        // Strip any original colliders under the Overpass donor hierarchy (we replace them)
        var overpassRoot = FindByFullPathIncludingInactive(OverpassRootPath);
        if (overpassRoot)
        {
            int removed = 0;
            foreach (var col in overpassRoot.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.Destroy(col);
                removed++;
            }
            MelonLogger.Msg("[Overpass] Removed " + removed + " original colliders.");
        }

        // Disable the road blockers (visual gates)
        foreach (var path in RoadblockerTargets)
        {
            var obj = FindByFullPathIncludingInactive(path);
            if (obj) obj.SetActive(false);
        }

        var rampRefParent = FindByFullPathIncludingInactive(OverpassRampParentForCollider);
        var topRefParent = FindByFullPathIncludingInactive(OverpassRootPath);

        // -------------------------
        // RC COLLIDERS: keep RC1 and RC2 (no triggers on these)
        // (These positions/rotations/scales are LOCAL to 'overpass_ramp' as before)
        // -------------------------
        if (rampRefParent)
        {
            // RC1
            SpawnCubeRelative(root, rampRefParent, "RC1",
                new Vector3(-26.3323f, -6.1683f, 15.29f),
                new Vector3(0f, 0f, 20f),
                new Vector3(32f, 1.5f, 10f),
                NextColor(), true);

            // RC2 (was RC3; renamed)
            SpawnCubeRelative(root, rampRefParent, "RC2",
                new Vector3(-39.4656f, -10.4992f, 15.2913f),
                new Vector3(0f, 0f, 10f),
                new Vector3(5f, 1.5f, 10f),
                NextColor(), true);
        }

        // -------------------------
        // TWO TELEPORT TRIGGERS — at WORLD transforms
        // -------------------------
        if (topRefParent)
        {
            // === OverpassDesertTrigger ===
            Vector3 wPos_Desert = new Vector3(-3.2099f, 10.5127f, -69.7335f);
            Vector3 wEuler_Desert = new Vector3(0f, 90f, 0f);
            Vector3 wScale_Desert = new Vector3(-0.1346f, 5.8455f, 10f);

            var desertTrigger = SpawnUsingWorldNumbers(root, topRefParent, "OverpassDesertTrigger",
                wPos_Desert, wEuler_Desert, wScale_Desert, NextColor(), false);

            MakeSelfTeleportTrigger(desertTrigger,
                new Vector3(-222.5477f, 41.9424f, -135.315f),
                new Vector3(0f, 76.27f, 0f));
            var tpDesert = desertTrigger.GetComponent<TeleportOnTrigger>();
            tpDesert.ActivateRootName = "DesertMap";
            tpDesert.DeactivateRootName = "Map";

            // === OverpassCityTrigger ===
            Vector3 wPos_City = new Vector3(-234.9709f, 42.1989f, -135.7764f);
            Vector3 wEuler_City = new Vector3(0f, 167f, 0f);
            Vector3 wScale_City = new Vector3(4.829f, 5.8455f, 10f);

            var cityTrigger = SpawnUsingWorldNumbers(root, topRefParent, "OverpassCityTrigger",
                wPos_City, wEuler_City, wScale_City, NextColor(), false);

            MakeSelfTeleportTrigger(cityTrigger,
                new Vector3(-3.5674f, 4.6717f, -57.3221f),
                new Vector3(-0f, 1.5f, 0f));
            var tpCity = cityTrigger.GetComponent<TeleportOnTrigger>();
            tpCity.ActivateRootName = "Map";
            tpCity.DeactivateRootName = "DesertMap";
        }

        MelonLogger.Msg($"[Overpass] Built RC1/RC2 and triggers. Debug rendering = {debugEnabled?.Value}");
    }

    // ---- Spawners & helpers ----

    // Ensures the generation root exists and is identity (so parenting with worldPositionStays=true won't skew transforms)
    private static GameObject EnsureRootIdentity()
    {
        var root = EnsureRoot();
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        return root;
    }

    private static GameObject EnsureRoot()
    {
        var root = GameObject.Find(RootName);
        if (root != null) return root;
        root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, SceneManager.GetActiveScene());
        return root;
    }

    private static void SpawnTestCube()
    {
        var root = EnsureRootIdentity();
        _spawnCounter++;
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = $"OverpassCollider_{_spawnCounter}";
        cube.transform.position = Vector3.zero;
        cube.transform.rotation = Quaternion.identity;
        cube.transform.localScale = Vector3.one;
        cube.transform.SetParent(root.transform, true);

        ApplyRenderer(cube, NextColor());

        var bc = cube.GetComponent<BoxCollider>() ?? cube.AddComponent<BoxCollider>();
        bc.isTrigger = false;

        MelonLogger.Msg($"[Overpass] Spawned {cube.name} (debug render={(debugEnabled != null && debugEnabled.Value)}).");
    }

    // Visible cube spawner using LOCAL TRS relative to a reference parent
    private static GameObject SpawnCubeRelative(GameObject root, GameObject refParent, string name,
        Vector3 localPos, Vector3 localEuler, Vector3 localScale, Color color, bool solid)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;

        ApplyRelativeWorldTRS(cube.transform, refParent.transform, localPos, localEuler, localScale);
        cube.transform.SetParent(root.transform, true);

        ApplyRenderer(cube, color);

        var bc = cube.GetComponent<BoxCollider>() ?? cube.AddComponent<BoxCollider>();
        bc.isTrigger = !solid;
        return cube;
    }

    // Visible cube spawner using WORLD TRS numbers (converted to local relative to reference)
    private static GameObject SpawnUsingWorldNumbers(GameObject genRoot, GameObject referenceParent, string name,
        Vector3 worldPos, Vector3 worldEuler, Vector3 worldScale, Color color, bool solid)
    {
        WorldToLocalTRS(referenceParent.transform, worldPos, worldEuler, worldScale,
            out var lp, out var le, out var ls);

        return SpawnCubeRelative(genRoot, referenceParent, name, lp, le, ls, color, solid);
    }

    // Apply (or hide) the renderer based on the debug preference
    private static void ApplyRenderer(GameObject go, Color color)
    {
        var r = go.GetComponent<Renderer>();
        if (!r) return;

        // When debug is enabled, show bright unlit color; otherwise, hide renderer.
        if (debugEnabled != null && debugEnabled.Value)
        {
            var sh = Shader.Find("Unlit/Color");
            if (sh != null)
            {
                r.material = new Material(sh);
                r.material.SetColor("_Color", color);
            }
            else
            {
                r.material.color = color;
            }

            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = true;
        }
        else
        {
            r.enabled = false; // invisible in-game when debug is false
        }
    }

    // Convert LOCAL (relative) to WORLD TRS
    private static void ApplyRelativeWorldTRS(Transform target, Transform reference, Vector3 localPos, Vector3 localEuler, Vector3 localScale)
    {
        if (!reference)
        {
            target.localPosition = localPos;
            target.localRotation = Quaternion.Euler(localEuler);
            target.localScale = localScale;
            return;
        }
        target.position = reference.TransformPoint(localPos);
        target.rotation = reference.rotation * Quaternion.Euler(localEuler);
        var refScale = reference.lossyScale;
        target.localScale = new Vector3(refScale.x * localScale.x, refScale.y * localScale.y, refScale.z * localScale.z);
    }

    // ---- WORLD → LOCAL conversion (so you can paste Inspector numbers directly) ----
    private static float SafeDiv(float a, float b) => Mathf.Approximately(b, 0f) ? 0f : a / b;

    private static void WorldToLocalTRS(Transform reference,
        Vector3 worldPos, Vector3 worldEuler, Vector3 worldScale,
        out Vector3 localPos, out Vector3 localEuler, out Vector3 localScale)
    {
        // Position
        localPos = reference ? reference.InverseTransformPoint(worldPos) : worldPos;

        // Rotation
        Quaternion worldQ = Quaternion.Euler(worldEuler);
        Quaternion localQ = reference ? Quaternion.Inverse(reference.rotation) * worldQ : worldQ;
        localEuler = localQ.eulerAngles;

        // Scale (component-wise)
        Vector3 refLossy = reference ? reference.lossyScale : Vector3.one;
        localScale = new Vector3(
            SafeDiv(worldScale.x, refLossy.x),
            SafeDiv(worldScale.y, refLossy.y),
            SafeDiv(worldScale.z, refLossy.z)
        );
    }

    // Make this object a teleport trigger to a target position
    // make this object a teleport trigger to a target position (and optional rotation)
    private static void MakeSelfTeleportTrigger(GameObject go, Vector3 targetPos, Vector3? targetEuler = null)
    {
        if (!go) return;

        var bc = go.GetComponent<BoxCollider>() ?? go.AddComponent<BoxCollider>();
        bc.isTrigger = true;

        var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
        rb.isKinematic = true; rb.useGravity = false;

        var tp = go.GetComponent<TeleportOnTrigger>() ?? go.AddComponent<TeleportOnTrigger>();
        tp.TargetPosition = targetPos;
        tp.HasRotation = targetEuler.HasValue;
        tp.TargetRotation = targetEuler.HasValue ? Quaternion.Euler(targetEuler.Value) : Quaternion.identity;
    }

    private static GameObject FindByFullPathIncludingInactive(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return null;
        var parts = fullPath.Split('/');
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            var scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != parts[0]) continue;
                Transform t = root.transform;
                for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
                if (t != null) return t.gameObject;
            }
        }
        return null;
    }
}

#if IL2CPP
// Il2Cpp MonoBehaviour: teleports *any* collider that enters to a target world position (default 0,0,0)
[RegisterTypeInIl2Cpp]
#endif
public class TeleportOnTrigger : MonoBehaviour
{
#if IL2CPP
    public TeleportOnTrigger(System.IntPtr ptr) : base(ptr) { }
    public TeleportOnTrigger() : base(ClassInjector.DerivedConstructorPointer<TeleportOnTrigger>())
    { ClassInjector.DerivedConstructorBody(this); }
#endif

    public Vector3 TargetPosition = Vector3.zero;
    public Quaternion TargetRotation = Quaternion.identity;
    public bool HasRotation = false;

    // Optional root toggles by name
    public string ActivateRootName = null;
    public string DeactivateRootName = null;

    private static GameObject FindRootByNameAllScenes(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        int count = SceneManager.sceneCount;
        for (int i = 0; i < count; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            var roots = scene.GetRootGameObjects(); // includes inactive roots
            for (int r = 0; r < roots.Length; r++)
            {
                var go = roots[r];
                if (go != null && go.name == name) return go;
            }
        }
        return null;
    }

    private void OnTriggerEnter(Collider other)
    {
        try
        {
            // find topmost parent and skip DesertMap roots
            Transform top = other.transform;
            while (top.parent != null) top = top.parent;
            if (top != null && top.name == "DesertMap") return;

            // teleport
            var rb = top.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = TargetPosition;
                if (HasRotation) rb.rotation = TargetRotation;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            else
            {
                top.position = TargetPosition;
                if (HasRotation) top.rotation = TargetRotation;
            }

            // toggle roots (use finder that sees inactive)
            if (!string.IsNullOrEmpty(ActivateRootName))
            {
                var go = FindRootByNameAllScenes(ActivateRootName);
                if (go != null) go.SetActive(true);
            }
            if (!string.IsNullOrEmpty(DeactivateRootName))
            {
                var go = FindRootByNameAllScenes(DeactivateRootName);
                if (go != null) go.SetActive(false);
            }
        }
        catch (System.Exception ex)
        {
            MelonLogger.Error("[Teleport] Error: " + ex);
        }
    }
}
