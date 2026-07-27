/**
 * Based on the InstallChecker from the Kethane mod for Kerbal Space Program.
 * https://github.com/Majiir/Kethane/blob/b93b1171ec42b4be6c44b257ad31c7efd7ea1702/Plugin/InstallChecker.cs
 *
 * Original is (C) Copyright Majiir.
 * CC0 Public Domain (http://creativecommons.org/publicdomain/zero/1.0/)
 */
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TimeForScience
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    internal class Startup : MonoBehaviour
    {
        private void Start()
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Debug.Log("[TimeForScience] Version " + version);
        }
    }

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class InstallChecker : MonoBehaviour
    {
        private const string MODNAME = "TimeForScience";
        private const string FOLDERNAME = "TimeForScience";
        private const string EXPECTEDPATH = FOLDERNAME + "/Plugins";

        protected void Start()
        {
            // Detect this mod's DLL loaded from anywhere other than the expected path
            // (also catches duplicate copies, since only one can be in the right place).
            var assemblies = AssemblyLoader.loadedAssemblies
                .Where(a => a.assembly.GetName().Name == Assembly.GetExecutingAssembly().GetName().Name)
                .Where(a => a.url != EXPECTEDPATH);

            if (assemblies.Any())
            {
                var badPaths = assemblies
                    .Select(a => a.path)
                    .Select(p => Uri.UnescapeDataString(new Uri(Path.GetFullPath(KSPUtil.ApplicationRootPath)).MakeRelativeUri(new Uri(p)).ToString().Replace('/', Path.DirectorySeparatorChar)));

                string message = MODNAME + " has been installed incorrectly and will not function properly. " +
                    "All files should be located in KSP/GameData/" + FOLDERNAME + ". Do not move any files from inside that folder.\n\n" +
                    "Incorrect path(s):\n" + string.Join("\n", badPaths.ToArray());

                PopupDialog.SpawnPopupDialog(
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    "TimeForScienceBadInstall",
                    "Incorrect " + MODNAME + " Installation",
                    message,
                    "OK",
                    false,
                    HighLogic.UISkin
                );
                Debug.LogError("[TimeForScience] " + message);
            }
        }
    }
}
