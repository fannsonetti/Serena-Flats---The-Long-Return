using MelonLoader;
using UnityEngine;

// Only entry point for MelonLoader
[assembly: MelonInfo(typeof(MainMod), "Desert Expansion", "0.1.1", "FannsoNetti")]
[assembly: MelonGame(null, null)]

public class MainMod : MelonMod
{
    public override void OnApplicationStart()
    {
        DesertModule.Initialize();
        OverpassModule.Initialize();
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        DesertModule.OnSceneLoaded(sceneName);
        OverpassModule.OnSceneLoaded(sceneName);
    }

    public override void OnUpdate()
    {
        OverpassModule.OnUpdate();
    }
}
