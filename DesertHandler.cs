using System.Collections;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public static class DesertModule
{
    private static bool desertLoaded = false;
    public static GameObject DesertRoot;

    // config
    private const string TargetSceneName = "Main";
    private const string DonorSceneName = "Tutorial";
    private const float delayBeforeLoad = 7f;
    private const float donorFindWindow = 5f;
    private const float uiCheckDelay = 3f;

    public static void Initialize()
    {
        MelonLogger.Msg("[Desert] Module initialized.");
    }

    public static void OnSceneLoaded(string sceneName)
    {
        if (desertLoaded) return;
        if (!string.Equals(sceneName, TargetSceneName, System.StringComparison.OrdinalIgnoreCase)) return;

        MelonLogger.Msg($"[Desert] Scene '{sceneName}' loaded — waiting {delayBeforeLoad} seconds before load…");
        MelonCoroutines.Start(DelayedStart());
    }

    public static void OnUpdate()
    {
        // no hotkey logic here anymore
    }

    private static IEnumerator DelayedStart()
    {
        yield return new WaitForSeconds(delayBeforeLoad);
        yield return LoadDesertAsync();
    }

    private static IEnumerator LoadDesertAsync()
    {
        if (desertLoaded) yield break;

        // remove blocking components from Main's "Map"
        TryRemoveBlockingMMapComponents("[Desert]");

        // load donor scene additively
        MelonLogger.Msg($"[Desert] Loading '{DonorSceneName}' additively…");
        var loadOp = SceneManager.LoadSceneAsync(DonorSceneName, LoadSceneMode.Additive);
        if (loadOp == null)
        {
            MelonLogger.Error($"[Desert] Failed to load {DonorSceneName} (LoadSceneAsync returned null).");
            yield break;
        }
        while (!loadOp.isDone) yield return null;
        yield return null; // settle

        // poll for donor scene with root "Map"
        var active = SceneManager.GetActiveScene();
        GameObject dmap = null;
        Scene chosenDonor = default;

        float t = 0f;
        while (t < donorFindWindow && dmap == null)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.IsValid() || !s.isLoaded || s == active) continue;

                foreach (var go in s.GetRootGameObjects())
                {
                    if (go != null && NameIsMap(go.name))
                    {
                        chosenDonor = s;
                        dmap = go;
                        break;
                    }
                }
                if (dmap != null) break;
            }

            if (dmap == null)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (dmap == null)
        {
            MelonLogger.Error("[Desert] Could not find a donor scene with a ROOT named 'Map'.");
            yield break;
        }

        MelonLogger.Msg($"[Desert] Using donor scene '{chosenDonor.name}'; found root '{dmap.name}'.");

        // ensure donor map doesn't keep blocker components (we can't have duplicates)
        TryRemoveBlockingComponentsOnRoot(dmap, "[Desert/donor]");

        // migrate and disable
        dmap.name = "DesertMap";
        try
        {
            SceneManager.MoveGameObjectToScene(dmap, active);
        }
        catch (System.Exception ex)
        {
            MelonLogger.Error("[Desert] MoveGameObjectToScene failed: " + ex);
            yield break;
        }

        dmap.SetActive(false);
        DesertRoot = dmap;
        desertLoaded = true;
        MelonLogger.Msg("[Desert] DesertMap moved to active scene and disabled.");

        // unload donor scene
        var unload = SceneManager.UnloadSceneAsync(chosenDonor);
        if (unload != null)
        {
            while (!unload.isDone) yield return null;
            MelonLogger.Msg("[Desert] Donor scene unloaded.");
        }

        // add & configure MapPositionUtility on Main/Map
        TryAddAndConfigureMapPositionUtility("[Desert]");

        // hud fix
        MelonCoroutines.Start(DelayedUICheck());
    }

    // === HUD enable/fix ===
    private static IEnumerator DelayedUICheck()
    {
        yield return new WaitForSeconds(uiCheckDelay);

        // Try exact path first, then by name, then legacy fallback "HUD_Canvas"
        var hudT =
            FindInActiveSceneByPath("UI/HUD")
            ?? FindInActiveSceneByName("HUD")
            ?? (GameObject.Find("HUD_Canvas") != null ? GameObject.Find("HUD_Canvas").transform : null);

        if (hudT == null)
        {
            MelonLogger.Warning("[Desert/UI] HUD not found at 'UI/HUD', by name 'HUD', or 'HUD_Canvas'.");
            yield break;
        }

        // Ensure the entire chain (e.g., UI -> HUD) is active
        EnsureHierarchyActive(hudT);

        // Enable Canvas and CanvasGroup if present
        var canvas = hudT.GetComponent<Canvas>();
        if (canvas != null) canvas.enabled = true;

        var cg = hudT.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        // Ensure an EventSystem exists (avoid Type[] ctor to prevent System.Type vs Il2CppSystem.Type mismatch)
        if (EventSystem.current == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
            MelonLogger.Msg("[Desert/UI] Created EventSystem.");
        }

        MelonLogger.Msg("[Desert/UI] HUD enabled at path: " + BuildHierarchyPath(hudT));
    }

    // --- helpers ---
    private static bool NameIsMap(string n)
        => string.Equals((n ?? "").Trim(), "Map", System.StringComparison.OrdinalIgnoreCase);

    private static void TryRemoveBlockingMMapComponents(string tag)
    {
        var active = SceneManager.GetActiveScene();
        if (!active.IsValid()) return;

        GameObject mmap = null;
        foreach (var go in active.GetRootGameObjects())
        {
            if (go != null && NameIsMap(go.name)) { mmap = go; break; }
        }
        if (mmap == null) return;

        var comps = mmap.GetComponents<Component>();
        if (comps == null || comps.Length == 0) return;

        string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (char.IsLetter(ch)) sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        string[] needleFulls =
        {
            "Il2CppScheduleOne.Map.Map",
            "Il2CppScheduleOne.Map.MapPositionUtility",
            "ScheduleOne.Map.Map",
            "ScheduleOne.Map.MapPositionUtility"
        };
        string[] needleNorms =
        {
            Norm("Il2CppScheduleOne.Map.Map"),
            Norm("Il2CppScheduleOne.Map.MapPositionUtility"),
            Norm("ScheduleOne.Map.Map"),
            Norm("ScheduleOne.Map.MapPositionUtility")
        };

        var lines = new System.Collections.Generic.List<string>(comps.Length);
        for (int i = 0; i < comps.Length; i++)
        {
            var c = comps[i];
            if (c == null) continue;
            var t = c.GetType();
            string full = t?.FullName ?? "";
            string name = t?.Name ?? "";
            string ns = t?.Namespace ?? "";
            string aq = t?.AssemblyQualifiedName ?? "";
            string toStr = c.ToString();
            lines.Add($"[{i}] Full='{full}' Name='{name}' NS='{ns}' AQ='{aq}' ToString='{toStr}'");
        }
        MelonLogger.Msg($"{tag} MMap components detail:\n" + string.Join("\n", lines.ToArray()));

        int removed = 0;
        for (int i = 0; i < comps.Length; i++)
        {
            var c = comps[i];
            if (c == null) continue;

            var t = c.GetType();
            string full = t?.FullName ?? "";
            string name = t?.Name ?? "";
            string ns = t?.Namespace ?? "";
            string aq = t?.AssemblyQualifiedName ?? "";
            string toStr = c.ToString();

            bool matchA = false;
            for (int k = 0; k < needleFulls.Length; k++)
            {
                if ((!string.IsNullOrEmpty(full) && full.Contains(needleFulls[k]))
                 || (!string.IsNullOrEmpty(aq) && aq.Contains(needleFulls[k])))
                { matchA = true; break; }
            }

            bool matchB =
                (!string.IsNullOrEmpty(ns) && ns.IndexOf("ScheduleOne.Map", System.StringComparison.OrdinalIgnoreCase) >= 0)
                && (string.Equals(name, "Map", System.StringComparison.Ordinal)
                    || string.Equals(name, "MapPositionUtility", System.StringComparison.Ordinal));

            bool matchC = (!string.IsNullOrEmpty(toStr) &&
                          (toStr.IndexOf("ScheduleOne.Map.MapPositionUtility", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || toStr.IndexOf("ScheduleOne.Map.Map", System.StringComparison.OrdinalIgnoreCase) >= 0));

            string fullN = Norm(full);
            string aqN = Norm(aq);
            string toN = Norm(toStr);
            bool matchD = false;
            for (int k = 0; k < needleNorms.Length; k++)
            {
                if ((!string.IsNullOrEmpty(fullN) && fullN.Contains(needleNorms[k]))
                 || (!string.IsNullOrEmpty(aqN) && aqN.Contains(needleNorms[k]))
                 || (!string.IsNullOrEmpty(toN) && toN.Contains(needleNorms[k])))
                { matchD = true; break; }
            }

            if (matchA || matchB || matchC || matchD)
            {
                UnityEngine.Object.Destroy(c);
                removed++;
                MelonLogger.Msg($"{tag} Removed blocking component: " + (string.IsNullOrEmpty(full) ? (string.IsNullOrEmpty(name) ? toStr : name) : full));
            }
        }

        if (removed == 0)
            MelonLogger.Msg($"{tag} Blocking components not removed (no match after expanded checks).");
        else
            MelonLogger.Msg($"{tag} Removed {removed} blocking component(s) from MMap.");
    }

    // === NEW: add & configure Il2CppScheduleOne.Map.MapPositionUtility on Main/Map ===
    private static void TryAddAndConfigureMapPositionUtility(string tag)
    {
        const double factor = 5.0064; // authoritative value

        var active = SceneManager.GetActiveScene();
        if (!active.IsValid())
        {
            MelonLogger.Warning($"{tag} Active scene invalid; skipping MapPositionUtility setup.");
            return;
        }

        // Find the root named "Map" in the active (Main) scene
        GameObject mmap = null;
        foreach (var go in active.GetRootGameObjects())
        {
            if (go != null && NameIsMap(go.name)) { mmap = go; break; }
        }
        if (mmap == null)
        {
            MelonLogger.Warning($"{tag} Could not find root 'Map' in active scene; MapPositionUtility not added.");
            return;
        }

        // Resolve Map/Hyland Point/EdgePoint & OriginPoint
        Transform container = mmap.transform.Find("Container");
        Transform edgePoint = container ? container.Find("EdgePoint") : null;
        Transform originPoint = container ? container.Find("OriginPoint") : null;
        if (container == null) MelonLogger.Warning($"{tag} 'Map=' not found.");
        if (edgePoint == null) MelonLogger.Warning($"{tag} 'Map/EdgePoint' not found.");
        if (originPoint == null) MelonLogger.Warning($"{tag} 'Map/OriginPoint' not found.");

        // Use IL2CPP generic overloads to avoid System.Type vs Il2CppSystem.Type mismatch
        Il2CppScheduleOne.Map.MapPositionUtility mpu = null;
        try
        {
            mpu = mmap.GetComponent<Il2CppScheduleOne.Map.MapPositionUtility>()
               ?? mmap.AddComponent<Il2CppScheduleOne.Map.MapPositionUtility>();
        }
        catch (System.Exception ex)
        {
            MelonLogger.Error($"{tag} Failed to get/add MapPositionUtility: {ex}");
            return;
        }
        if (mpu == null)
        {
            MelonLogger.Error($"{tag} MapPositionUtility component ended up null.");
            return;
        }

        // Set conversion factor on instance property if available (float or double)
        bool setInstanceProp = false;
        var t = typeof(Il2CppScheduleOne.Map.MapPositionUtility);
        try
        {
            var p = t.GetProperty("conversionFactor", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
            if (p != null)
            {
                var setter = p.GetSetMethod(true);
                if (setter != null)
                {
                    var isStatic = setter.IsStatic;
                    if (p.PropertyType == typeof(float))
                    {
                        if (isStatic) p.SetValue(null, (float)factor, null);
                        else { p.SetValue(mpu, (float)factor, null); setInstanceProp = true; }
                    }
                    else if (p.PropertyType == typeof(double))
                    {
                        if (isStatic) p.SetValue(null, factor, null);
                        else { p.SetValue(mpu, factor, null); setInstanceProp = true; }
                    }
                }
            }
        }
        catch { /* ignore and fall back */ }

        // Also try the private backing field name (instance or static)
        try
        {
            var f = t.GetField("_conversionFactor_k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
            if (f != null)
            {
                object boxed = (f.FieldType == typeof(float)) ? (object)(float)factor : (f.FieldType == typeof(double)) ? (object)factor : null;
                if (boxed != null)
                {
                    if (f.IsStatic) f.SetValue(null, boxed);
                    else if (!setInstanceProp) f.SetValue(mpu, boxed); // prefer property if we successfully set it
                }
            }
        }
        catch { /* ignored */ }

        // Assign EdgePoint / OriginPoint on instance
        try { mpu.EdgePoint = edgePoint; } catch { }
        try { mpu.OriginPoint = originPoint; } catch { }

        MelonLogger.Msg($"{tag} MapPositionUtility set: factor={factor}, EdgePoint={(edgePoint ? edgePoint.name : "null")}, OriginPoint={(originPoint ? originPoint.name : "null")} ");
    }

    // Remove blocker components (Map/MapPositionUtility in ScheduleOne namespace) from a specific Map root
    private static void TryRemoveBlockingComponentsOnRoot(GameObject root, string tag)
    {
        if (root == null) return;
        var comps = root.GetComponents<Component>();
        if (comps == null || comps.Length == 0) return;

        string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (char.IsLetter(ch)) sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        string[] needleFulls =
        {
            "Il2CppScheduleOne.Map.Map",
            "Il2CppScheduleOne.Map.MapPositionUtility",
            "ScheduleOne.Map.Map",
            "ScheduleOne.Map.MapPositionUtility"
        };
        string[] needleNorms =
        {
            Norm("Il2CppScheduleOne.Map.Map"),
            Norm("Il2CppScheduleOne.Map.MapPositionUtility"),
            Norm("ScheduleOne.Map.Map"),
            Norm("ScheduleOne.Map.MapPositionUtility")
        };

        int removed = 0;
        foreach (var c in comps)
        {
            if (c == null) continue;
            var t = c.GetType();
            string full = t?.FullName ?? string.Empty;
            string name = t?.Name ?? string.Empty;
            string ns = t?.Namespace ?? string.Empty;
            string aq = t?.AssemblyQualifiedName ?? string.Empty;
            string toStr = c.ToString();

            bool matchA = false;
            for (int k = 0; k < needleFulls.Length; k++)
            {
                if ((!string.IsNullOrEmpty(full) && full.Contains(needleFulls[k]))
                 || (!string.IsNullOrEmpty(aq) && aq.Contains(needleFulls[k])))
                { matchA = true; break; }
            }

            bool matchB = (!string.IsNullOrEmpty(ns) && ns.IndexOf("ScheduleOne.Map", System.StringComparison.OrdinalIgnoreCase) >= 0)
                           && (string.Equals(name, "Map", System.StringComparison.Ordinal)
                               || string.Equals(name, "MapPositionUtility", System.StringComparison.Ordinal));

            string fullN = Norm(full);
            string aqN = Norm(aq);
            string toN = Norm(toStr);
            bool matchD = false;
            for (int k = 0; k < needleNorms.Length; k++)
            {
                if ((!string.IsNullOrEmpty(fullN) && fullN.Contains(needleNorms[k]))
                 || (!string.IsNullOrEmpty(aqN) && aqN.Contains(needleNorms[k]))
                 || (!string.IsNullOrEmpty(toN) && toN.Contains(needleNorms[k])))
                { matchD = true; break; }
            }

            if (matchA || matchB || matchD)
            {
                UnityEngine.Object.Destroy(c);
                removed++;
                MelonLogger.Msg($"{tag} Removed blocker from '{root.name}': " + (string.IsNullOrEmpty(full) ? (string.IsNullOrEmpty(name) ? toStr : name) : full));
            }
        }

        if (removed == 0)
            MelonLogger.Msg($"{tag} No blockers removed on '{root.name}'.");
        else
            MelonLogger.Msg($"{tag} Removed {removed} blocker component(s) from '{root.name}'.");
    }

    // ---- UI helper methods ----
    private static Transform FindInActiveSceneByPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var active = SceneManager.GetActiveScene();
        if (!active.IsValid()) return null;

        var parts = path.Split('/');
        if (parts.Length == 0) return null;

        var roots = active.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null) continue;
            if (!string.Equals(root.name, parts[0], System.StringComparison.OrdinalIgnoreCase)) continue;

            Transform t = root.transform;
            for (int k = 1; k < parts.Length && t != null; k++)
                t = t.Find(parts[k]);

            if (t != null) return t;
        }
        return null;
    }

    private static Transform FindInActiveSceneByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var active = SceneManager.GetActiveScene();
        if (!active.IsValid()) return null;

        var roots = active.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var r = roots[i];
            if (r == null) continue;
            if (string.Equals(r.name, name, System.StringComparison.OrdinalIgnoreCase)) return r.transform;

            var all = r.GetComponentsInChildren<Transform>(true); // include inactive
            for (int j = 0; j < all.Length; j++)
            {
                var t = all[j];
                if (t != null && string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase))
                    return t;
            }
        }
        return null;
    }

    private static void EnsureHierarchyActive(Transform t)
    {
        if (t == null) return;
        // activate parents first, then self
        var stack = new System.Collections.Generic.Stack<Transform>();
        for (var p = t; p != null; p = p.parent) stack.Push(p);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node != null && !node.gameObject.activeSelf) node.gameObject.SetActive(true);
        }
    }

    private static string BuildHierarchyPath(Transform t)
    {
        if (t == null) return "null";
        var stack = new System.Collections.Generic.Stack<string>();
        for (var p = t; p != null; p = p.parent) stack.Push(p.name);
        var sb = new System.Text.StringBuilder(64);
        bool first = true;
        while (stack.Count > 0)
        {
            if (!first) sb.Append('/');
            sb.Append(stack.Pop());
            first = false;
        }
        return sb.ToString();
    }
}
