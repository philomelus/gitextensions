using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GitExtUtils.GitUI;
using GitUI.Properties;
using JetBrains.Annotations;
using Microsoft.WindowsAPICodePack.Taskbar;
using ResourceManager;

namespace GitUI
{
    // NOTE do not make this class abstract as it breaks the WinForms designer in VS

    /// <summary>Base class for a Git Extensions <see cref="Form"/>.</summary>
    /// <remarks>Includes support for font, hotkey, icon, translation, and position restore.</remarks>
    public class GitExtensionsForm : GitExtensionsFormBase
    {
        /// <summary>indicates whether the <see cref="Form"/>'s position will be restored</summary>
        private readonly bool _enablePositionRestore;

        /// <summary>Creates a new <see cref="GitExtensionsForm"/> without position restore.</summary>
        public GitExtensionsForm()
            : this(false)
        {
        }

        /// <summary>Creates a new <see cref="GitExtensionsForm"/> indicating position restore.</summary>
        /// <param name="enablePositionRestore">Indicates whether the <see cref="Form"/>'s position
        /// will be restored upon being re-opened.</param>
        protected GitExtensionsForm(bool enablePositionRestore)
        {
            _enablePositionRestore = enablePositionRestore;

            Icon = Resources.git_extensions_logo_final;
            FormClosing += GitExtensionsForm_FormClosing;

            var cancelButton = new Button();
            cancelButton.Click += CancelButtonClick;

            CancelButton = cancelButton;
        }

        public virtual void CancelButtonClick(object sender, EventArgs e)
        {
            Close();
        }

        private void GitExtensionsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_enablePositionRestore)
            {
                SavePosition(GetType().Name);
            }

            if (GitCommands.Utils.EnvUtils.RunningOnWindows() && TaskbarManager.IsPlatformSupported)
            {
                try
                {
                    TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress);
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        #region icon

        /// <summary>Specifies a Git Extensions' color index.</summary>
        protected enum ColorIndex
        {
            Default,
            Blue,
            Green,
            LightBlue,
            Purple,
            Red,
            Yellow,
            Unknown = -1
        }

        protected static ColorIndex GetColorIndexByName(string color)
        {
            switch (color)
            {
                case "default":
                    return ColorIndex.Default;
                case "blue":
                    return ColorIndex.Blue;
                case "green":
                    return ColorIndex.Green;
                case "lightblue":
                    return ColorIndex.LightBlue;
                case "purple":
                    return ColorIndex.Purple;
                case "red":
                    return ColorIndex.Red;
                case "yellow":
                    return ColorIndex.Yellow;
                case "random":
                    return (ColorIndex)new Random(DateTime.Now.Millisecond).Next(7);
            }

            return ColorIndex.Unknown;
        }

        #endregion icon

        protected override void OnLoad(EventArgs e)
        {
            if (_enablePositionRestore)
            {
                RestorePosition(GetType().Name);
            }

            // Should be called after restoring position
            base.OnLoad(e);

            if (!IsDesignModeComponent(this))
            {
                OnRuntimeLoad(e);
            }
        }

        /// <summary>Invoked at runtime during the <see cref="OnLoad"/> method.</summary>
        protected virtual void OnRuntimeLoad(EventArgs e)
        {
        }

        private bool _windowCentred;

        /// <summary>
        ///   Restores the position of a form from the user settings. Does
        ///   nothing if there is no entry for the form in the settings, or the
        ///   setting would be invisible on the current display configuration.
        /// </summary>
        /// <param name = "name">The name to use when looking up the position in
        ///   the settings</param>
        private void RestorePosition(string name)
        {
            if (!Visible ||
                WindowState == FormWindowState.Minimized)
            {
                return;
            }

            _windowCentred = StartPosition == FormStartPosition.CenterParent;

            var position = LookupWindowPosition(name);
            if (position == null)
            {
                return;
            }

            _needsPositionRestore = false;

            SuspendLayout();

            StartPosition = FormStartPosition.Manual;

            if (FormBorderStyle == FormBorderStyle.Sizable ||
                FormBorderStyle == FormBorderStyle.SizableToolWindow)
            {
                Size = DpiUtil.Scale(position.Rect.Size, originalDpi: position.DeviceDpi);
            }

            if (Owner == null || !_windowCentred)
            {
                var location = DpiUtil.Scale(position.Rect.Location, originalDpi: position.DeviceDpi);

                if (FindWindowScreen(location) is Rectangle rect)
                {
                    location.Y = rect.Y;
                }

                DesktopLocation = location;
            }
            else
            {
                // Calculate location for modal form with parent
                Location = new Point(Owner.Left + (Owner.Width / 2) - (Width / 2),
                    Math.Max(0, Owner.Top + (Owner.Height / 2) - (Height / 2)));
            }

            if (WindowState != position.State)
            {
                WindowState = position.State;
            }

            ResumeLayout();
        }

        private static Rectangle? FindWindowScreen(Point location)
        {
            SortedDictionary<float, Rectangle> distance = new SortedDictionary<float, Rectangle>();
            foreach (var rect in from screen in Screen.AllScreens
                                 select screen.WorkingArea)
            {
                if (rect.Contains(location) && !distance.ContainsKey(0.0f))
                {
                    return null; // title in screen
                }

                int midPointX = rect.X + (rect.Width / 2);
                int midPointY = rect.Y + (rect.Height / 2);
                float d = (float)Math.Sqrt(((location.X - midPointX) * (location.X - midPointX)) +
                    ((location.Y - midPointY) * (location.Y - midPointY)));
                distance.Add(d, rect);
            }

            if (distance.Count > 0)
            {
                return distance.First().Value;
            }
            else
            {
                return null;
            }
        }

        private static WindowPositionList _windowPositionList;

        /// <summary>
        ///   Save the position of a form to the user settings. Hides the window
        ///   as a side-effect.
        /// </summary>
        /// <param name = "name">The name to use when writing the position to the
        ///   settings</param>
        private void SavePosition(string name)
        {
            try
            {
                var rectangle =
                    WindowState == FormWindowState.Normal
                        ? DesktopBounds
                        : RestoreBounds;

                var formWindowState =
                    WindowState == FormWindowState.Maximized
                        ? FormWindowState.Maximized
                        : FormWindowState.Normal;

                // Write to the user settings:
                if (_windowPositionList == null)
                {
                    _windowPositionList = WindowPositionList.Load();
                }

                WindowPosition windowPosition = _windowPositionList.Get(name);

                // Don't save location when we center modal form
                if (windowPosition != null && Owner != null && _windowCentred)
                {
                    if (rectangle.Width <= windowPosition.Rect.Width && rectangle.Height <= windowPosition.Rect.Height)
                    {
                        rectangle.Location = windowPosition.Rect.Location;
                    }
                }

                var position = new WindowPosition(rectangle, DpiUtil.DpiX, formWindowState, name);
                _windowPositionList.AddOrUpdate(position);
                _windowPositionList.Save();
            }
            catch (Exception)
            {
                // TODO: howto restore a corrupted config?
            }
        }

        /// <summary>
        ///   Looks up a window in the user settings and returns its saved position.
        /// </summary>
        /// <param name = "name">The name.</param>
        /// <returns>
        ///   The saved window position if it exists. Null if the entry
        ///   doesn't exist, or would not be visible on any screen in the user's
        ///   current display setup.
        /// </returns>
        [CanBeNull]
        private static WindowPosition LookupWindowPosition(string name)
        {
            try
            {
                if (_windowPositionList == null)
                {
                    _windowPositionList = WindowPositionList.Load();
                }

                var position = _windowPositionList?.Get(name);
                if (position == null || position.Rect.IsEmpty)
                {
                    return null;
                }

                if (Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(position.Rect)))
                {
                    return position;
                }
            }
            catch (Exception)
            {
                // TODO: howto restore a corrupted config?
            }

            return null;
        }
    }
}
