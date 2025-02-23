﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using _1RM.Model;
using _1RM.Service;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.PageHost;
using Stylet;
using Markdig.Wpf;
using _1RM.Controls.NoteDisplay;
using _1RM.Model.Protocol.Base;
using _1RM.View.Launcher;

namespace _1RM.View
{
    public class LauncherWindowViewModel : NotifyPropertyChangedBaseScreen
    {
        public const double LAUNCHER_LIST_AREA_WIDTH = 400;
        public const double LAUNCHER_GRID_KEYWORD_HEIGHT = 46;
        public const double LAUNCHER_SERVER_LIST_ITEM_HEIGHT = 40;
        public const double LAUNCHER_ACTION_LIST_ITEM_HEIGHT = 34;
        public const double LAUNCHER_OUTLINE_CORNER_RADIUS = 8;
        public static readonly CornerRadius LauncherOutlineCornerRadiusObj = new CornerRadius(LAUNCHER_OUTLINE_CORNER_RADIUS, LAUNCHER_OUTLINE_CORNER_RADIUS, LAUNCHER_OUTLINE_CORNER_RADIUS, LAUNCHER_OUTLINE_CORNER_RADIUS);


        public const int MAX_SERVER_COUNT = 8;
        public const double MAX_SELECTION_HEIGHT = LauncherWindowViewModel.LAUNCHER_SERVER_LIST_ITEM_HEIGHT * MAX_SERVER_COUNT;
        public const double MAX_WINDOW_HEIGHT = LauncherWindowViewModel.LAUNCHER_GRID_KEYWORD_HEIGHT + MAX_SELECTION_HEIGHT;

        private Visibility _serverSelectionsViewVisibility = Visibility.Visible;
        public Visibility ServerSelectionsViewVisibility
        {
            get => _serverSelectionsViewVisibility;
            set => SetAndNotifyIfChanged(ref _serverSelectionsViewVisibility, value);
        }

        public ServerSelectionsViewModel ServerSelectionsViewModel { get; } = IoC.Get<ServerSelectionsViewModel>();
        public QuickConnectionViewModel QuickConnectionViewModel { get; } = IoC.Get<QuickConnectionViewModel>();

        #region properties


        private double _gridMainHeight;
        public double GridMainHeight
        {
            get => _gridMainHeight;
            set
            {
                if (SetAndNotifyIfChanged(ref _gridMainHeight, value))
                {
                    GridMainClip = new RectangleGeometry(new Rect(new Size(LAUNCHER_LIST_AREA_WIDTH, GridMainHeight)), LAUNCHER_OUTLINE_CORNER_RADIUS, LAUNCHER_OUTLINE_CORNER_RADIUS);
                }
            }
        }


        private RectangleGeometry? _gridMainClip = null;
        public RectangleGeometry? GridMainClip
        {
            get => _gridMainClip;
            set => SetAndNotifyIfChanged(ref _gridMainClip, value);
        }


        public double GridNoteHeight { get; }

        private double _noteWidth = 500;

        public double NoteWidth
        {
            get => _noteWidth;
            private set => SetAndNotifyIfChanged(ref _noteWidth, value);
        }

        #endregion

        public LauncherWindowViewModel()
        {
            GridNoteHeight = MAX_WINDOW_HEIGHT + 20;
        }

        protected override void OnViewLoaded()
        {
            HideMe();
            if (this.View is LauncherWindowView window)
            {
                ServerSelectionsViewModel.Init(this);
                QuickConnectionViewModel.Init(this);

                ServerSelectionsViewVisibility = Visibility.Visible;
                ReSetWindowHeight(false);
                SetHotKey();
                ServerSelectionsViewModel.NoteField = window.NoteField;
                window.Deactivated += (s, a) => { HideMe(); };
                window.KeyDown += (s, a) => { if (a.Key == Key.Escape) HideMe(); };
                //ServerSelectionsViewModel.CalcVisibleByFilter();
                ServerSelectionsViewModel.CalcNoteFieldVisibility();
            }
        }


        public void ReSetWindowHeight(bool showGridAction)
        {
            double height;
            if (ServerSelectionsViewVisibility == Visibility.Visible)
            {
                height = ServerSelectionsViewModel.ReCalcGridMainHeight(showGridAction);
            }
            else
            {
                height = QuickConnectionViewModel.ReCalcGridMainHeight();
            }

            Execute.OnUIThread(() =>
            {
                GridMainHeight = height;
            });
        }


        public void ShowMe()
        {
            if (this.View is LauncherWindowView window)
            {
                SimpleLogHelper.Debug($"Call shortcut to invoke launcher Visibility = {window.Visibility}");
                if (IoC.Get<MainWindowViewModel>().TopLevelViewModel != null) return;
                if (IoC.Get<ConfigurationService>().Launcher.LauncherEnabled == false) return;

                lock (this)
                {
                    window.WindowState = WindowState.Normal;
                    ServerSelectionsViewModel.Filter = "";
                    ServerSelectionsViewModel.CalcVisibleByFilter();
                    QuickConnectionViewModel.SelectedProtocol = QuickConnectionViewModel.Protocols.First();

                    // show position
                    var p = ScreenInfoEx.GetMouseSystemPosition();
                    var screenEx = ScreenInfoEx.GetCurrentScreenBySystemPosition(p);
                    window.Top = screenEx.VirtualWorkingAreaCenter.Y - GridMainHeight / 2 - 40; // 40: margin of BorderMainContent
                    window.Left = screenEx.VirtualWorkingAreaCenter.X - window.BorderMainContent.ActualWidth / 2;

                    var noteWidth = (screenEx.VirtualWorkingArea.Width - window.BorderMainContent.ActualWidth - 100) / 2;
                    if (noteWidth < 100)
                        noteWidth = 100;
                    NoteWidth = Math.Min(noteWidth, NoteWidth);

                    window.Show();
                    window.Visibility = Visibility.Visible;
                    window.Activate();
                    window.Topmost = true; // important
                    window.Topmost = false; // important
                    window.Topmost = true; // important
                    window.Focus(); // important
                    ServerSelectionsViewModel.TbKeyWord.Focus();
                }
            }
            else
            {
                IoC.Get<IWindowManager>().ShowWindow(this);
            }
        }


        public void HideMe()
        {
            if (this.View is LauncherWindowView window)
            {
                lock (this)
                {
                    Execute.OnUIThread(() =>
                    {
                        window.Hide();
                        if (ServerSelectionsViewModel != null)
                        {
                            ServerSelectionsViewModel.Filter = "";
                            ServerSelectionsViewModel.GridMenuActions.Visibility = Visibility.Hidden;
                            ServerSelectionsViewVisibility = Visibility.Visible;
                            ServerSelectionsViewModel.CalcNoteFieldVisibility();
                        }

                        //if (QuickConnectionViewModel != null)
                        //{
                        //    QuickConnectionViewModel.Filter = "";
                        //}

                        // After startup and initalizing our application and when closing our window and minimize the application to tray we free memory with the following line:
                        System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet = System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet;
                    });
                }
            }
        }


        public void SetHotKey()
        {
            GlobalEventHelper.OnLauncherHotKeyChanged -= SetHotKey;
            if (this.View is LauncherWindowView window)
            {
                GlobalHotkeyHooker.Instance.Unregist(window);
                if (IoC.Get<ConfigurationService>().Launcher.LauncherEnabled == false)
                    return;
                var r = GlobalHotkeyHooker.Instance.Register(window, (uint)IoC.Get<ConfigurationService>().Launcher.HotKeyModifiers, IoC.Get<ConfigurationService>().Launcher.HotKeyKey, this.ShowMe);
                switch (r.Item1)
                {
                    case GlobalHotkeyHooker.RetCode.Success:
                        break;
                    case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_NOT_REGISTERED:
                        {
                            var msg = $"{IoC.Get<ILanguageService>().Translate("hotkey_registered_fail")}: {r.Item2}";
                            SimpleLogHelper.Warning(msg);
                            MessageBoxHelper.Warning(msg, useNativeBox: true);
                            break;
                        }
                    case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_ALREADY_REGISTERED:
                        {
                            var msg = $"{IoC.Get<ILanguageService>().Translate("hotkey_already_registered")}: {r.Item2}";
                            SimpleLogHelper.Warning(msg);
                            MessageBoxHelper.Warning(msg, useNativeBox: true);
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException(r.Item1.ToString());
                }
            }
            GlobalEventHelper.OnLauncherHotKeyChanged += SetHotKey;
        }

        public void ToggleQuickConnection()
        {
            if (ServerSelectionsViewVisibility == Visibility.Collapsed)
            {
                ServerSelectionsViewModel.Show();
            }
            else
            {
                QuickConnectionViewModel.Show();
            }
            ReSetWindowHeight(false);
        }
    }
}