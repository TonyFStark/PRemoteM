﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using _1RM.Controls;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Stylet;

namespace _1RM.View.ServerList
{
    public class TagsPanelViewModel : NotifyPropertyChangedBaseScreen
    {
        public GlobalData AppData { get; }
        public TagsPanelViewModel(GlobalData appData)
        {
            AppData = appData;
        }


        private bool _filterIsFocused = false;
        public bool FilterIsFocused
        {
            get => _filterIsFocused;
            set => SetAndNotifyIfChanged(ref _filterIsFocused, value);
        }

        private readonly DebounceDispatcher _debounceDispatcher = new();
        private string _filterString = "";
        public string FilterString
        {
            get => _filterString;
            set
            {
                // can only be called by the Ui
                if (SetAndNotifyIfChanged(ref _filterString, value))
                {
                    _debounceDispatcher.Debounce(150, (obj) =>
                    {
                        if (_filterString == FilterString)
                        {
                            if (this.View is TagsPanelView v)
                            {
                                Execute.OnUIThread(() => { CollectionViewSource.GetDefaultView(v.ListBoxTags.ItemsSource).Refresh(); });
                            }
                        }
                    });
                }
            }
        }



        private RelayCommand? _cmdTagDelete;
        public RelayCommand CmdTagDelete
        {
            get
            {
                return _cmdTagDelete ??= new RelayCommand((o) =>
                {
                    if (o is not Tag obj)
                        return;

                    var protocolServerBases = AppData.VmItemList.Where(x => x.Server.Tags.Contains(obj.Name) && x.IsEditable).Select(x => x.Server).ToArray();

                    if (protocolServerBases.Any() != true)
                    {
                        return;
                    }

                    if (false == MessageBoxHelper.Confirm(IoC.Get<ILanguageService>().Translate("confirm_to_delete")))
                        return;

                    foreach (var server in protocolServerBases)
                    {
                        if (server.Tags.Contains(obj.Name))
                        {
                            server.Tags.Remove(obj.Name);
                        }
                    }
                    AppData.UpdateServer(protocolServerBases);
                    var tagFilters = IoC.Get<ServerListPageViewModel>().TagFilters;
                    var delete = tagFilters.FirstOrDefault(x => x.TagName == obj.Name);
                    if (delete != null)
                    {
                        var tmp = tagFilters.ToList();
                        tmp.Remove(delete);
                        tagFilters = new List<TagFilter>(tmp);
                    }
                });
            }
        }





        private RelayCommand? _cmdTagRename;
        public RelayCommand CmdTagRename
        {
            get
            {
                return _cmdTagRename ??= new RelayCommand((o) =>
                {
                    if (o is not Tag obj)
                        return;

                    string oldTagName = obj.Name;

                    var protocolServerBases = AppData.VmItemList.Where(x => x.Server.Tags.Contains(oldTagName) && x.IsEditable).Select(x => x.Server).ToArray();

                    if (protocolServerBases.Any() != true)
                    {
                        return;
                    }

                    string newTagName = InputWindow.InputBox(IoC.Get<ILanguageService>().Translate("Tags"), IoC.Get<ILanguageService>().Translate("Tags"), obj.Name);
                    newTagName = TagAndKeywordEncodeHelper.RectifyTagName(newTagName);
                    if (string.IsNullOrEmpty(newTagName) || oldTagName == newTagName)
                        return;

                    foreach (var server in protocolServerBases)
                    {
                        if (server.Tags.Contains(oldTagName))
                        {
                            server.Tags.Remove(oldTagName);
                            server.Tags.Add(newTagName);
                        }
                    }
                    AppData.UpdateServer(protocolServerBases);


                    // restore selected scene
                    var tagFilters = IoC.Get<ServerListPageViewModel>().TagFilters;
                    var rename = tagFilters.FirstOrDefault(x => x.TagName == oldTagName);
                    if (rename != null)
                    {
                        var renamed = TagFilter.Create(newTagName, rename.Type);
                        var tmp = tagFilters.ToList();
                        tmp.Remove(rename);
                        tmp.Add(renamed);
                        IoC.Get<ServerListPageViewModel>().TagFilters = new List<TagFilter>(tmp);
                    }

                    // restore display scene
                    if (AppData.TagList.Any(x => x.Name == newTagName))
                    {
                        AppData.TagList.First(x => x.Name == newTagName).IsPinned = obj.IsPinned;
                    }
                });
            }
        }



        private RelayCommand? _cmdTagConnect;
        public RelayCommand CmdTagConnect
        {
            get
            {
                return _cmdTagConnect ??= new RelayCommand((o) =>
                {
                    if (o is not Tag obj)
                        return;

                    foreach (var vmProtocolServer in AppData.VmItemList.ToArray())
                    {
                        if (vmProtocolServer.Server.Tags.Contains(obj.Name))
                        {
                            GlobalEventHelper.OnRequestServerConnect?.Invoke(vmProtocolServer.Id);
                            Thread.Sleep(100);
                        }
                    }
                });
            }
        }



        private RelayCommand? _cmdTagConnectToNewTab;
        public RelayCommand CmdTagConnectToNewTab
        {
            get
            {
                return _cmdTagConnectToNewTab ??= new RelayCommand((o) =>
                {
                    if (o is not Tag obj)
                        return;

                    var token = DateTime.Now.Ticks.ToString();
                    foreach (var vmProtocolServer in AppData.VmItemList.ToArray())
                    {
                        if (vmProtocolServer.Server.Tags.Contains(obj.Name))
                        {
                            GlobalEventHelper.OnRequestServerConnect?.Invoke(vmProtocolServer.Id, token);
                            Thread.Sleep(100);
                        }
                    }
                });
            }
        }
    }
}
