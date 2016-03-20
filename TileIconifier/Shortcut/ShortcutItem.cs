﻿using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TileIconifier.Custom;
using TileIconifier.Utilities;

namespace TileIconifier.Shortcut
{
    [Serializable]
    public class ShortcutItem
    {
        private Image _mediumImageCache;
        private byte[] _mediumImageCacheBytes;
        private Image _smallImageCache;
        private byte[] _smallImageCacheBytes;
        private Bitmap _standardIcon;

        public ShortcutItem(FileInfo shortcutFileInfo)
        {
            ShortcutFileInfo = shortcutFileInfo;
            LoadParameters();
        }

        public ShortcutItem(string shortcutPath)
        {
            ShortcutFileInfo = new FileInfo(shortcutPath);
            LoadParameters();
        }

        public bool IsTileIconifierCustomShortcut => new DirectoryInfo(TargetFolderPath).Parent?.FullName + "\\" ==
                                                     CustomShortcutGetters.CustomShortcutVbsPath;

        public ShortcutUser ShortcutUser
        {
            get
            {
                if (ShortcutFileInfo.FullName.StartsWith(CustomShortcutGetters.CustomShortcutAllUsersPath))
                    return ShortcutUser.AllUsers;
                if (ShortcutFileInfo.FullName.StartsWith(CustomShortcutGetters.CustomShortcutCurrentUserPath))
                    return ShortcutUser.CurrentUser;
                return ShortcutUser.Unknown;
            }
        }

        public bool IsValidForIconification => !string.IsNullOrEmpty(TargetFilePath) && File.Exists(TargetFilePath);

        public bool IsIconified => File.Exists(VisualElementManifestPath)
                                   && Directory.Exists(VisualElementsPath)
                                   && File.Exists(FullMediumIconPath)
                                   && File.Exists(FullSmallIconPath);

        public Bitmap StandardIcon
        {
            get
            {
                if (_standardIcon != null) return _standardIcon;
                try
                {
                    _standardIcon = Icon.ExtractAssociatedIcon(ShortcutFileInfo.FullName)?.ToBitmap();
                }
                catch
                {
                    // ignored
                }
                return _standardIcon;
            }
        }

        public byte[] MediumImageBytes
        {
            get { return NewParameters.MediumImageBytes ?? OldParameters?.MediumImageBytes; }
            set { NewParameters.MediumImageBytes = value; }
        }

        public byte[] SmallImageBytes
        {
            get { return NewParameters.SmallImageBytes ?? OldParameters?.SmallImageBytes; }
            set { NewParameters.SmallImageBytes = value; }
        }


        public string BackgroundColor
        {
            get { return NewParameters.BackgroundColor; }
            set { NewParameters.BackgroundColor = value; }
        }

        public string ForegroundText
        {
            get { return NewParameters.ForegroundText; }
            set { NewParameters.ForegroundText = value; }
        }

        public bool ShowNameOnSquare150X150Logo
        {
            get { return NewParameters.ShowNameOnSquare150X150Logo == "on"; }
            set { NewParameters.ShowNameOnSquare150X150Logo = value ? "on" : "off"; }
        }

        public FileInfo ShortcutFileInfo { get; set; }
        public string AppId { get; set; }
        public bool? IsPinned { get; set; }
        private ShortcutIconParameters OldParameters { get; set; }
        private ShortcutIconParameters NewParameters { get; set; }

        public bool HasUnsavedChanges => !NewParameters.Equals(OldParameters);
        public bool MediumImageChange => !NewParameters.MediumImageBytesEqual(OldParameters);
        public bool SmallImageChange => !NewParameters.SmallImageBytesEqual(OldParameters);
        public bool ForegroundTextColourChanged => NewParameters.ForegroundText != OldParameters.ForegroundText;

        public Image MediumImage()
        {
            if (_mediumImageCacheBytes == MediumImageBytes) return _mediumImageCache;

            if (_mediumImageCacheBytes == MediumImageBytes && _mediumImageCacheBytes.SequenceEqual(MediumImageBytes))
                return _mediumImageCache;

            _mediumImageCache = ImageUtils.ByteArrayToImage(MediumImageBytes);
            _mediumImageCacheBytes = MediumImageBytes?.ToArray();

            return _mediumImageCache;
        }

        public Image SmallImage()
        {
            if (_smallImageCacheBytes == SmallImageBytes) return _smallImageCache;

            if (_smallImageCacheBytes == SmallImageBytes && _smallImageCacheBytes.SequenceEqual(SmallImageBytes))
                return _smallImageCache;

            _smallImageCache = ImageUtils.ByteArrayToImage(SmallImageBytes);
            _smallImageCacheBytes = SmallImageBytes?.ToArray();

            return _smallImageCache;
        }

        private void LoadParameters()
        {
            IsPinned = null;
            if (IsIconified)
            {
                var xmlDoc = XDocument.Load(VisualElementManifestPath);

                try
                {
                    var parameters = from b in xmlDoc.Descendants("VisualElements")
                        select new ShortcutIconParameters
                        {
                            BackgroundColor = b.Attribute("BackgroundColor").Value,
                            ForegroundText = b.Attribute("ForegroundText").Value,
                            ShowNameOnSquare150X150Logo = b.Attribute("ShowNameOnSquare150x150Logo").Value,
                            MediumImageBytes =
                                ImageUtils.LoadBitmapToByteArray(TargetFolderPath +
                                                                 b.Attribute("Square150x150Logo").Value),
                            SmallImageBytes =
                                ImageUtils.LoadBitmapToByteArray(TargetFolderPath + b.Attribute("Square70x70Logo").Value)
                        };
                    OldParameters = parameters.Single();
                    NewParameters = OldParameters.Clone();
                }
                catch
                {
                    ResetParameters();
                }
            }
            else
            {
                ResetParameters();
            }
        }

        public void UndoChanges()
        {
            NewParameters = OldParameters.Clone();
        }

        public void CommitChanges()
        {
            OldParameters = NewParameters.Clone();
        }

        public void ResetParameters()
        {
            //defaults
            OldParameters = new ShortcutIconParameters
            {
                BackgroundColor = "black",
                ForegroundText = "light",
                ShowNameOnSquare150X150Logo = "on"
            };
            NewParameters = OldParameters.Clone();
        }

        #region Path properties

        private string _targetFilePath;

        public string TargetFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(_targetFilePath))
                    _targetFilePath = ShortcutUtils.ResolveShortcut(ShortcutFileInfo.FullName);
                return
                    Environment.ExpandEnvironmentVariables("%PATHEXT%").Split(';').Any(
                        e =>
                            string.Equals(Path.GetExtension(_targetFilePath), e,
                                StringComparison.InvariantCultureIgnoreCase))
                        ? _targetFilePath
                        : null;
            }
        }

        public string VisualElementManifestPath =>
            $"{TargetFolderPath}{Path.GetFileNameWithoutExtension(TargetFilePath)}.VisualElementsManifest.xml";

        public string TargetFolderPath => Path.GetDirectoryName(TargetFilePath) + "\\";

        public string VisualElementsPath => TargetFolderPath + @"\VisualElements\";

        public string MediumIconName
            => $"MediumIcon{Path.GetFileNameWithoutExtension(TargetFilePath)}.png";

        public string RelativeMediumIconPath
            => $"{Path.GetFileName(Path.GetDirectoryName(VisualElementsPath))}\\{MediumIconName}";

        public string FullMediumIconPath => $"{VisualElementsPath}\\{MediumIconName}";

        public string SmallIconName
            => $"SmallIcon{Path.GetFileNameWithoutExtension(TargetFilePath)}.png";

        public string RelativeSmallIconPath
            => $"{Path.GetFileName(Path.GetDirectoryName(VisualElementsPath))}\\{SmallIconName}";

        public string FullSmallIconPath => $"{VisualElementsPath}\\{SmallIconName}";

        #endregion
    }
}