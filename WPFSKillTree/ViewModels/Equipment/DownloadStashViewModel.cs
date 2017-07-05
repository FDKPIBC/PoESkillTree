﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using POESKillTree.Common.ViewModels;
using POESKillTree.Controls;
using POESKillTree.Controls.Dialogs;
using POESKillTree.Localization;
using POESKillTree.Model;
using POESKillTree.Model.Builds;
using POESKillTree.Model.Items;
using POESKillTree.Utils;

namespace POESKillTree.ViewModels.Equipment
{
    public class DownloadStashViewModel : CloseableViewModel
    {
        private readonly StashViewModel _stash;
        private readonly IPersistentData _persistenData;
        private readonly IDialogCoordinator _dialogCoordinator;
        private readonly TaskCompletionSource<object> _viewLoadedCompletionSource;

        private PoEBuild _build;
        public PoEBuild Build
        {
            get { return _build; }
            private set { SetProperty(ref _build, value); }
        }

        private string _tabsLink;
        public string TabsLink
        {
            get { return _tabsLink; }
            private set { SetProperty(ref _tabsLink, value);}
        }

        private string _tabLink;
        public string TabLink
        {
            get { return _tabLink; }
            private set { SetProperty(ref _tabLink, value); }
        }

        private readonly List<StashBookmark> _tabs = new List<StashBookmark>();

        public ICollectionView TabsView { get; }

        public static NotifyingTask<IReadOnlyList<string>> CurrentLeagues { get; private set; }

        private RelayCommand<string> _openInBrowserCommand;
        public ICommand OpenInBrowserCommand
        {
            get
            {
                return _openInBrowserCommand ?? (_openInBrowserCommand = new RelayCommand<string>(
                    param => Process.Start(param), 
                    param => !string.IsNullOrEmpty(param)));
            }
        }

        private ICommand _loadTabsCommand;
        public ICommand LoadTabsCommand
        {
            get { return _loadTabsCommand ?? (_loadTabsCommand = new AsyncRelayCommand(LoadTabs)); }
        }

        private ICommand _loadTabContentsCommand;
        public ICommand LoadTabContentsCommand
        {
            get { return _loadTabContentsCommand ?? (_loadTabContentsCommand = new AsyncRelayCommand(LoadTabContents)); }
        }

        public DownloadStashViewModel(IDialogCoordinator dialogCoordinator, IPersistentData persistentData, StashViewModel stash)
        {
            _stash = stash;
            _persistenData = persistentData;
            _dialogCoordinator = dialogCoordinator;
            DisplayName = L10n.Message("Download & Import Stash");
            Build = persistentData.CurrentBuild;

            if (Build.League != null && _persistenData.LeagueStashes.ContainsKey(Build.League))
                _tabs = new List<StashBookmark>(_persistenData.LeagueStashes[Build.League]);
            TabsView = new ListCollectionView(_tabs);
            TabsView.CurrentChanged += (sender, args) => UpdateTabLink();

            Build.PropertyChanged += BuildOnPropertyChanged;
            BuildOnPropertyChanged(this, null);

            _viewLoadedCompletionSource = new TaskCompletionSource<object>();
            if (CurrentLeagues == null)
            {
                CurrentLeagues = new NotifyingTask<IReadOnlyList<string>>(LoadCurrentLeaguesAsync(),
                    async e =>
                    {
                        await _viewLoadedCompletionSource.Task;
                        await _dialogCoordinator.ShowWarningAsync(this,
                            L10n.Message("Could not load the currently running leagues."), e.Message);
                    });
            }
        }

        // Errors in CurrentLeagues task may only be shown when the dialog coordinator context is registered,
        // which requires the view to be loaded.
        public void ViewLoaded()
        {
            _viewLoadedCompletionSource.SetResult(null);
        }

        private static async Task<IReadOnlyList<string>> LoadCurrentLeaguesAsync()
        {
            using (var client = new HttpClient())
            {
                var file = await client.GetStringAsync("http://api.pathofexile.com/leagues?type=main&compact=1")
                    .ConfigureAwait(false);
                return JArray.Parse(file).Select(t => t["id"].Value<string>()).ToList();
            }
        }

        protected override void OnClose()
        {
            Build.PropertyChanged -= BuildOnPropertyChanged;
        }

        private void BuildOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (propertyChangedEventArgs != null && propertyChangedEventArgs.PropertyName == "League")
            {
                _tabs.Clear();
                if (Build.League != null && _persistenData.LeagueStashes.ContainsKey(Build.League))
                {
                    _tabs.AddRange(_persistenData.LeagueStashes[Build.League]);
                }
                TabsView.Refresh();
                TabsView.MoveCurrentToFirst();
            }
            TabsLink =
                string.Format(
                    "https://www.pathofexile.com/character-window/get-stash-items?tabs=1&tabIndex=0&league={0}&accountName={1}",
                    Build.League, Build.AccountName);
            UpdateTabLink();
        }

        private void UpdateTabLink()
        {
            var selectedTab = TabsView.CurrentItem as StashBookmark;
            if (selectedTab == null)
            {
                TabLink = "";
                return;
            }
            TabLink =
                string.Format(
                    "https://www.pathofexile.com/character-window/get-stash-items?tabs=0&tabIndex={2}&league={0}&accountName={1}",
                    Build.League, Build.AccountName, selectedTab.Position);
        }

        private async Task LoadTabs()
        {
            var stashData = Clipboard.GetText();
            _tabs.Clear();
            try
            {
                var tabs = (JArray)JObject.Parse(stashData)["tabs"];
                foreach (var tab in tabs)
                {
                    if (tab["hidden"].Value<bool>())
                        continue;
                    var name = tab["n"].Value<string>();
                    var index = tab["i"].Value<int>();
                    var c = tab["colour"].Value<JObject>();
                    var color = Color.FromArgb(0xFF, c["r"].Value<byte>(), c["g"].Value<byte>(), c["b"].Value<byte>());
                    _tabs.Add(new StashBookmark(name, index, color));
                }
                if (Build.League != null)
                {
                    _persistenData.LeagueStashes[Build.League] = new List<StashBookmark>(_tabs);
                }
            }
            catch (Exception e)
            {
                await _dialogCoordinator.ShowErrorAsync(this,
                    L10n.Message("An error occurred while attempting to load stash data."), e.Message);
            }
            TabsView.Refresh();
            TabsView.MoveCurrentToFirst();
        }

        private async Task LoadTabContents()
        {
            var tabContents = Clipboard.GetText();
            try
            {
                var json = JObject.Parse(tabContents);
                var isQuadTab = json.Value<bool>("quadLayout");
                var items = new List<Item>();
                foreach (JObject jItem in json["items"])
                {
                    if (isQuadTab)
                    {
                        // icons of quad tabs are downsized and their url doesn't allow inferring the normal-sized url
                        jItem.Remove("icon");
                    }
                    items.Add(new Item(_persistenData, jItem));
                }

                var yStart = _stash.LastOccupiedRow + 3;

                var selectedBookmark = TabsView.CurrentItem as StashBookmark;
                var sb = selectedBookmark != null
                    ? new StashBookmark(selectedBookmark.Name, yStart, selectedBookmark.Color)
                    : new StashBookmark("imported", yStart);

                _stash.BeginUpdate();
                _stash.AddStashTab(sb);

                var yOffsetInImported = items.Min(i => i.Y);
                var yMax = items.Max(i => i.Y + i.Height);
                foreach (var item in items)
                {
                    item.Y += yStart - yOffsetInImported;
                    if (item.X + item.Width > StashViewModel.Columns)
                    {
                        // Mostly for quad stash tabs:
                        // - add items on the right side below those on the left side
                        // - items crossing both sides have to be moved to one side, which might lead to stacked items
                        // Also makes sure items are not added outside the stash when importing other special tabs.
                        item.X = Math.Max(0, Math.Min(item.X - StashViewModel.Columns, StashViewModel.Columns - 1));
                        item.Y += yMax;
                    }
                    _stash.AddItem(item, true);
                }

                await _dialogCoordinator.ShowInfoAsync(this, L10n.Message("New tab added"),
                    string.Format(L10n.Message("New tab with {0} items was added to stash."), items.Count));
            }
            catch (Exception e)
            {
                await _dialogCoordinator.ShowErrorAsync(this,
                    L10n.Message("An error occurred while attempting to load stash data."), e.Message);
            }
            finally
            {
                _stash.EndUpdate();
            }
        }
    }
}