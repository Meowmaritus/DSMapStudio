﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;

namespace StudioCore
{
    /// <summary>
    /// Generic asset description for a generic game asset
    /// </summary>
    public class AssetDescription
    {
        /// <summary>
        /// Pretty UI friendly name for an asset. Usually the file name without an extention i.e. c1234
        /// </summary>
        public string AssetName = null;

        /// <summary>
        /// Absolute path of where the full asset is located. If this asset exists in a mod override directory,
        /// then this path points to that instead of the base game asset.
        /// </summary>
        public string AssetPath = null;

        public string AssetArchiveVirtualPath = null;

        /// <summary>
        /// Virtual friendly path for this asset to use with the resource manager
        /// </summary>
        public string AssetVirtualPath = null;

        /// <summary>
        /// Where applicable, the numeric asset ID. Usually applies to chrs, objs, and various map pieces
        /// </summary>
        public int AssetID;

        public override int GetHashCode()
        {
            if (AssetVirtualPath != null)
            {
                return AssetVirtualPath.GetHashCode();
            }
            else if (AssetPath != null)
            {
                return AssetPath.GetHashCode();
            }
            return base.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is AssetDescription ad)
            {
                if (AssetVirtualPath != null)
                {
                    return AssetVirtualPath.Equals(ad.AssetVirtualPath);
                }
                if (AssetPath != null)
                {
                    return AssetPath.Equals(ad.AssetPath);
                }
            }
            return base.Equals(obj);
        }
    }

    /// <summary>
    /// Exposes an interface to retrieve game assets from the various souls games. Also allows layering
    /// of an additional mod directory on top of the game assets.
    /// </summary>
    public class AssetLocator
    {

        public static readonly string GameExecutatbleFilter =
            "Windows executable (*.EXE) |*.EXE*|" +
            "Playstation executable (*.BIN) |*.BIN*|" +
            "All Files|*.*";

        public GameType Type { get; private set; } = GameType.Undefined;

        /// <summary>
        /// The game interroot where all the game assets are
        /// </summary>
        public string GameRootDirectory { get; private set; } = null;

        /// <summary>
        /// An optional override mod directory where modded files are stored
        /// </summary>
        public string GameModDirectory { get; private set; } = null;

        public AssetLocator()
        {
            Type = CFG.Current.Game_Type;
            if (CFG.Current.Interroot_Directory != "")
            {
                GameRootDirectory = CFG.Current.Interroot_Directory;
            }
        }

        private List<string> FullMapList = null;

        /// <summary>
        /// Sets the game root directory by giving a path to the game exe/eboot.bin. Autodetects the game type.
        /// </summary>
        /// <param name="exePath">Path to an exe/eboot.bin</param>
        /// <returns>true if the game was autodetected</returns>
        public bool SetGameRootDirectoryByExePath(string exePath)
        {
            GameRootDirectory = Path.GetDirectoryName(exePath);
            if (exePath.ToLower().Contains("darksouls.exe"))
            {
                Type = GameType.DarkSoulsPTDE;
            }
            else if (exePath.ToLower().Contains("darksoulsremastered.exe"))
            {
                Type = GameType.DarkSoulsRemastered;
            }
            else if (exePath.ToLower().Contains("darksoulsii.exe"))
            {
                Type = GameType.DarkSoulsIISOTFS;
            }
            else if (exePath.ToLower().Contains("darksoulsiii.exe"))
            {
                Type = GameType.DarkSoulsIII;
            }
            else if (exePath.ToLower().Contains("eboot.bin"))
            {
                if (Directory.Exists($@"{GameRootDirectory}\dvdroot_ps4"))
                {
                    Type = GameType.Bloodborne;
                    GameRootDirectory = GameRootDirectory + $@"\dvdroot_ps4";
                }
                else
                {
                    Type = GameType.DemonsSouls;
                }
            }
            else if (exePath.ToLower().Contains("sekiro.exe"))
            {
                Type = GameType.Sekiro;
            }
            else
            {
                GameRootDirectory = null;
            }

            // Invalidate various caches
            FullMapList = null;

            // Save config
            if (GameRootDirectory != null)
            {
                CFG.Current.Interroot_Directory = GameRootDirectory;
                CFG.Current.Game_Type = Type;
            }

            return true;
        }

        /// <summary>
        /// Gets the full list of maps in the game (excluding chalice dungeons). Basically if there's an msb for it,
        /// it will be in this list.
        /// </summary>
        /// <returns></returns>
        public List<string> GetFullMapList()
        {
            if (GameRootDirectory == null)
            {
                return null;
            }

            if (FullMapList != null)
            {
                return FullMapList;
            }

            var mapSet = new HashSet<string>();

            // DS2 has its own structure for msbs, where they are all inside individual folders
            if (Type == GameType.DarkSoulsIISOTFS)
            {
                var maps = Directory.GetFileSystemEntries(GameRootDirectory + @"\map", @"m*").ToList();
                if (GameModDirectory != null)
                {
                    maps.AddRange(Directory.GetFileSystemEntries(GameModDirectory + @"\map", @"m*").ToList());
                }
                foreach (var map in maps)
                {
                    mapSet.Add(Path.GetFileNameWithoutExtension($@"{map}.blah"));
                }
            }
            else
            {
                var msbFiles = Directory.GetFileSystemEntries(GameRootDirectory + @"\map\MapStudio\", @"*.msb")
                    .Select(Path.GetFileNameWithoutExtension).ToList();
                msbFiles.AddRange(Directory.GetFileSystemEntries(GameRootDirectory + @"\map\MapStudio\", @"*.msb.dcx")
                    .Select(Path.GetFileNameWithoutExtension).Select(Path.GetFileNameWithoutExtension).ToList());
                if (GameModDirectory != null)
                {
                    msbFiles.AddRange(Directory.GetFileSystemEntries(GameModDirectory + @"\map\MapStudio\", @"*.msb")
                    .Select(Path.GetFileNameWithoutExtension).ToList());
                    msbFiles.AddRange(Directory.GetFileSystemEntries(GameModDirectory + @"\map\MapStudio\", @"*.msb.dcx")
                        .Select(Path.GetFileNameWithoutExtension).Select(Path.GetFileNameWithoutExtension).ToList());
                }
                foreach (var msb in msbFiles)
                {
                    mapSet.Add(msb);
                }
            }
            var mapRegex = new Regex(@"^m\d{2}_\d{2}_\d{2}_\d{2}$");
            FullMapList = mapSet.Where(x => mapRegex.IsMatch(x)).ToList();
            FullMapList.Sort();
            return FullMapList;
        }

        public AssetDescription GetMapMSB(string mapid)
        {
            AssetDescription ad = new AssetDescription();
            if (Type == GameType.DarkSoulsIISOTFS)
            {
            }
            else
            {
                var path = $@"\map\MapStudio\{mapid}";
                if (File.Exists($@"{GameModDirectory}\{path}.msb.dcx"))
                {
                    ad.AssetPath = $@"{GameModDirectory}\{path}.msb.dcx";
                }
                else if (File.Exists($@"{GameRootDirectory}\{path}.msb.dcx"))
                {
                    ad.AssetPath = $@"{GameRootDirectory}\{path}.msb.dcx";
                }
                else if (File.Exists($@"{GameModDirectory}\{path}.msb"))
                {
                    ad.AssetPath = $@"{GameModDirectory}\{path}.msb";
                }
                else if (File.Exists($@"{GameRootDirectory}\{path}.msb"))
                {
                    ad.AssetPath = $@"{GameRootDirectory}\{path}.msb";
                }
            }
            ad.AssetName = mapid;
            return ad;
        }

        public List<AssetDescription> GetMapModels(string mapid)
        {
            var ret = new List<AssetDescription>();
            if (Type == GameType.DarkSoulsIII || Type == GameType.Sekiro)
            {
                var mapfiles = Directory.GetFileSystemEntries(GameRootDirectory + $@"\map\{mapid}\", @"*.mapbnd.dcx").ToList();
                foreach (var f in mapfiles)
                {
                    var ad = new AssetDescription();
                    ad.AssetPath = f;
                    var name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(f));
                    ad.AssetName = name;
                    ad.AssetArchiveVirtualPath = $@"map/{mapid}/model/{name}";
                    ad.AssetVirtualPath = $@"map/{mapid}/model/{name}/{name}.flver";
                    ret.Add(ad);
                }
            }
            else
            {
                var mapfiles = Directory.GetFileSystemEntries(GameRootDirectory + $@"\map\{mapid}\", @"*.flver").ToList();
                foreach (var f in mapfiles)
                {
                    var ad = new AssetDescription();
                    ad.AssetPath = f;
                    var name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(f));
                    ad.AssetName = name;
                    //ad.AssetArchiveVirtualPath = $@"map/{mapid}/model/{name}";
                    ad.AssetVirtualPath = $@"map/{mapid}/model/{name}/{name}.flver";
                    ret.Add(ad);
                }
            }
            return ret;
        }

        public string MapModelNameToAssetName(string mapid, string modelname)
        {
            if (Type == GameType.DarkSoulsPTDE || Type == GameType.DarkSoulsRemastered)
            {
                return $@"{modelname}A{mapid.Substring(1, 2)}";
            }
            return $@"{mapid}_{modelname.Substring(1)}";
        }

        public AssetDescription GetMapModel(string mapid, string model)
        {
            var ret = new AssetDescription();
            if (Type == GameType.DarkSoulsPTDE)
            {
                ret.AssetPath = $@"{GameRootDirectory}\map\{mapid}\{model}.flver";
            }
            else
            {
                ret.AssetPath = $@"{GameRootDirectory}\map\{mapid}\{model}.mapbnd.dcx";
            }
            ret.AssetName = model;
            ret.AssetArchiveVirtualPath = $@"map/{mapid}/model/{model}";
            ret.AssetVirtualPath = $@"map/{mapid}/model/{model}/{model}.flver";
            return ret;
        }

        public AssetDescription GetMapCollisionModel(string mapid, string model)
        {
            var ret = new AssetDescription();
            if (Type == GameType.DarkSoulsPTDE)
            {
                ret.AssetPath = $@"{GameRootDirectory}\map\{mapid}\{model}.hkx";
                ret.AssetName = model;
                ret.AssetVirtualPath = $@"map/{mapid}/hit/hi/{model}.hkx";
            }
            else
            {
                return GetNullAsset();
            }
            return ret;
        }

        public AssetDescription GetMapNVMModel(string mapid, string model)
        {
            var ret = new AssetDescription();
            if (Type == GameType.DarkSoulsPTDE)
            {
                ret.AssetPath = $@"{GameRootDirectory}\map\{mapid}\{model}.nvm";
                ret.AssetName = model;
                ret.AssetArchiveVirtualPath = $@"map/{mapid}/nav";
                ret.AssetVirtualPath = $@"map/{mapid}/nav/{model}.nvm";
            }
            else
            {
                return GetNullAsset();
            }
            return ret;
        }

        public AssetDescription GetChrModel(string chr)
        {
            var ret = new AssetDescription();
            ret.AssetName = chr;
            ret.AssetArchiveVirtualPath = $@"chr/{chr}/model";
            ret.AssetVirtualPath = $@"chr/{chr}/model/{chr}.flver";
            return ret;
        }

        public AssetDescription GetObjModel(string obj)
        {
            var ret = new AssetDescription();
            ret.AssetName = obj;
            ret.AssetArchiveVirtualPath = $@"obj/{obj}/model";
            ret.AssetVirtualPath = $@"obj/{obj}/model/{obj}.flver";
            return ret;
        }

        public AssetDescription GetNullAsset()
        {
            var ret = new AssetDescription();
            ret.AssetPath = "null";
            ret.AssetName = "null";
            ret.AssetArchiveVirtualPath = "null";
            ret.AssetVirtualPath = "null";
            return ret;
        }

        /// <summary>
        /// Converts a virtual path to an actual filesystem path. Only resolves virtual paths up to the bnd level,
        /// which the remaining string is output for additional handling
        /// </summary>
        /// <param name="virtualPath"></param>
        /// <returns></returns>
        public string VirtualToRealPath(string virtualPath, out string bndpath)
        {
            var pathElements = virtualPath.Split('/');
            var mapRegex = new Regex(@"^m\d{2}_\d{2}_\d{2}_\d{2}$");
            string ret = "";

            // Parse the virtual path with a DFA and convert it to a game path
            int i = 0;
            if (pathElements[i].Equals("map"))
            {
                i++;
                if (mapRegex.IsMatch(pathElements[i]))
                {
                    var mapid = pathElements[i];
                    i++;
                    if (pathElements[i].Equals("model"))
                    {
                        i++;
                        bndpath = "";
                        if (Type == GameType.DarkSoulsPTDE)
                        {
                            return $@"{GameRootDirectory}\map\{mapid}\{pathElements[i]}.flver";
                        }
                        return $@"{GameRootDirectory}\map\{mapid}\{pathElements[i]}.mapbnd.dcx";
                    }
                    else if (pathElements[i].Equals("hit"))
                    {
                        i++;
                        var hittype = pathElements[i];
                        i++;
                        if (Type == GameType.DarkSoulsPTDE)
                        {
                            bndpath = "";
                            return $@"{GameRootDirectory}\map\{mapid}\{pathElements[i]}";
                        }
                        bndpath = "";
                        return null;
                    }
                    else if (pathElements[i].Equals("nav"))
                    {
                        i++;
                        if (Type == GameType.DarkSoulsPTDE)
                        {
                            if (i < pathElements.Length)
                            {
                                bndpath = $@"{pathElements[i]}";
                            }
                            else
                            {
                                bndpath = "";
                            }
                            return $@"{GameRootDirectory}\map\{mapid}\{mapid}.nvmbnd";
                        }
                        bndpath = "";
                        return null;
                    }
                }
            }
            else if (pathElements[i].Equals("chr"))
            {
                i++;
                var chrid = pathElements[i];
                i++;
                if (pathElements[i].Equals("model"))
                {
                    bndpath = "";
                    if (Type == GameType.DarkSoulsPTDE)
                    {
                        return $@"{GameRootDirectory}\chr\{chrid}.chrbnd";
                    }
                    return $@"{GameRootDirectory}\chr\{chrid}.chrbnd.dcx";
                }
            }
            else if (pathElements[i].Equals("obj"))
            {
                i++;
                var chrid = pathElements[i];
                i++;
                if (pathElements[i].Equals("model"))
                {
                    bndpath = "";
                    if (Type == GameType.DarkSoulsPTDE)
                    {
                        return $@"{GameRootDirectory}\obj\{chrid}.objbnd";
                    }
                    return $@"{GameRootDirectory}\obj\{chrid}.objbnd.dcx";
                }
            }

            bndpath = virtualPath;
            return null;
        }

        public string GetBinderVirtualPath(string virtualPathToBinder, string binderFilePath)
        {
            var filename = Path.GetFileNameWithoutExtension($@"{binderFilePath}.blah");
            if (filename.Length > 0)
            {
                filename = $@"{virtualPathToBinder}/{filename}";
            }
            else
            {
                filename = virtualPathToBinder;
            }
            return filename;
        }
    }
}
