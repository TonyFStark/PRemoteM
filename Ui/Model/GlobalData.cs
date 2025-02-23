﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using _1RM.Model.DAO;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.View;
using Shawn.Utils;
using Stylet;
using ServerListPageViewModel = _1RM.View.ServerList.ServerListPageViewModel;

namespace _1RM.Model
{
    public class GlobalData : NotifyPropertyChangedBase
    {
        private readonly Timer _timer;
        private bool _isTimerStopFlag = false;
        public GlobalData(ConfigurationService configurationService)
        {
            _configurationService = configurationService;
            ConnectTimeRecorder.Init(AppPathHelper.Instance.ConnectTimeRecord);
            ReloadServerList();

            _timer = new Timer(30 * 1000)
            {
                AutoReset = false,
            };
            _timer.Elapsed += TimerOnElapsed;
            _timer.Start();
        }

        private void TimerOnElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                var mainWindowViewModel = IoC.Get<MainWindowViewModel>();
                var listPageViewModel = IoC.Get<ServerListPageViewModel>();
                var launcherWindowViewModel = IoC.Get<LauncherWindowViewModel>();
                // do not reload when any selected / launcher is shown / editor view is show
                if (mainWindowViewModel.EditorViewModel != null
                    || listPageViewModel.ServerListItems.Any(x => x.IsSelected)
                    || launcherWindowViewModel.View?.IsVisible == true)
                {
                    return;
                }

#if DEBUG
                SimpleLogHelper.Debug("check database update.");
#endif
                ReloadServerList();
            }
            finally
            {
                if (_isTimerStopFlag == false && _configurationService.DatabaseCheckPeriod > 0)
                {
                    _timer.Interval = _configurationService.DatabaseCheckPeriod * 1000;
                    _timer.Start();
                }
            }
        }

        private DataSourceService? _sourceService;
        private readonly ConfigurationService _configurationService;

        public void SetDbOperator(DataSourceService sourceService)
        {
            _sourceService = sourceService;
        }


        private ObservableCollection<Tag> _tagList = new ObservableCollection<Tag>();
        public ObservableCollection<Tag> TagList
        {
            get => _tagList;
            private set => SetAndNotifyIfChanged(ref _tagList, value);
        }



        #region Server Data

        public Action? VmItemListDataChanged;

        public List<ProtocolBaseViewModel> VmItemList { get; set; } = new List<ProtocolBaseViewModel>();


        private void ReadTagsFromServers()
        {
            var pinnedTags = _configurationService.PinnedTags;

            // get distinct tag from servers
            var tags = new List<Tag>();
            foreach (var tagNames in VmItemList.Select(x => x.Server.Tags))
            {
                foreach (var tagName in tagNames)
                {
                    if (tags.All(x => x.Name != tagName))
                        tags.Add(new Tag(tagName, pinnedTags.Contains(tagName), SaveOnPinnedChanged) { ItemsCount = 1 });
                    else
                        tags.First(x => x.Name == tagName).ItemsCount++;
                }
            }

            TagList = new ObservableCollection<Tag>(tags.OrderBy(x => x.Name));
        }

        private void SaveOnPinnedChanged()
        {
            _configurationService.PinnedTags = TagList.Where(x => x.IsPinned).Select(x => x.Name).ToList();
            _configurationService.Save();
        }

        public void ReloadServerList(bool focus = false)
        {
            if (_sourceService == null)
            {
                return;
            }


            var needRead = false;
            if (focus == false)
            {
                needRead = _sourceService.LocalDataSource?.NeedRead() ?? false;
                foreach (var additionalSource in _sourceService.AdditionalSources)
                {
                    // 对于断线的数据源，隔一段时间后尝试重连
                    if (additionalSource.Value.Status == EnumDbStatus.LostConnection)
                    {
                        if (additionalSource.Value.StatueTime.AddMinutes(10) < DateTime.Now
                            && additionalSource.Value.Database_OpenConnection())
                        {
                            additionalSource.Value.Database_SelfCheck();
                        }
                        continue;
                    }

                    if (needRead == false)
                    {
                        needRead |= additionalSource.Value.NeedRead();
                    }
                }
            }

            if (focus || needRead)
            {
                // read from db
                VmItemList = _sourceService.GetServers(focus);
                ConnectTimeRecorder.Cleanup();
                ReadTagsFromServers();
                VmItemListDataChanged?.Invoke();
            }
        }

        public void UnselectAllServers()
        {
            foreach (var item in VmItemList)
            {
                item.IsSelected = false;
            }
        }

        public void AddServer(ProtocolBase protocolServer, DataSourceBase dataSource)
        {
            dataSource.Database_InsertServer(protocolServer);
            ReloadServerList();
        }

        public void UpdateServer(ProtocolBase protocolServer)
        {
            Debug.Assert(string.IsNullOrEmpty(protocolServer.Id) == false);
            if (_sourceService == null) return;
            var source = _sourceService.GetDataSource(protocolServer.DataSourceName);
            if (source == null || source.IsWritable == false) return;
            UnselectAllServers();
            source.Database_UpdateServer(protocolServer);
            int i = VmItemList.Count;
            {
                var old = VmItemList.FirstOrDefault(x => x.Id == protocolServer.Id && x.Server.DataSourceName == source.DataSourceName);
                if (old != null
                    && old.Server != protocolServer)
                {
                    i = VmItemList.IndexOf(old);
                    VmItemList.Remove(old);
                    VmItemList.Insert(i, new ProtocolBaseViewModel(protocolServer, source));
                }
            }

            ReloadServerList();
        }

        public void UpdateServer(IEnumerable<ProtocolBase> protocolServers)
        {
            if (_sourceService == null) return;
            var groupedServers = protocolServers.GroupBy(x => x.DataSourceName);
            foreach (var groupedServer in groupedServers)
            {
                var source = _sourceService.GetDataSource(groupedServer.First().DataSourceName);
                if (source?.IsWritable == true)
                    source.Database_UpdateServer(groupedServer);
            }
            ReloadServerList();
        }

        public void DeleteServer(ProtocolBase protocolServer)
        {
            if (_sourceService == null) return;
            Debug.Assert(string.IsNullOrEmpty(protocolServer.Id) == false);
            if (_sourceService == null) return;
            var source = _sourceService.GetDataSource(protocolServer.DataSourceName);
            if (source == null || source.IsWritable == false) return;
            if (source.Database_DeleteServer(protocolServer.Id))
            {
                ReloadServerList();
            }
        }

        public void DeleteServer(IEnumerable<ProtocolBase> protocolServers)
        {
            if (_sourceService == null) return;
            var groupedServers = protocolServers.GroupBy(x => x.DataSourceName);
            foreach (var groupedServer in groupedServers)
            {
                var source = _sourceService.GetDataSource(groupedServer.First().DataSourceName);
                if (source?.IsWritable == true)
                    source.Database_DeleteServer(groupedServer.Select(x => x.Id));
            }
            ReloadServerList();
        }

        #endregion Server Data

        public void StopTick()
        {
            _timer.Stop();
            _isTimerStopFlag = true;
        }
        public void StartTick()
        {
            _isTimerStopFlag = false;
            ReloadServerList();
            if (_timer.Enabled == false && _configurationService.DatabaseCheckPeriod > 0)
            {
                _timer.Interval = _configurationService.DatabaseCheckPeriod * 1000;
                _timer.Start();
            }
        }
    }
}