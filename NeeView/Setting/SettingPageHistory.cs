﻿using NeeLaboratory.Windows.Input;
using NeeView.Windows.Property;
using System;
using System.Collections.Generic;
using System.Windows;

namespace NeeView.Setting
{
    public class SettingPageHistory : SettingPage
    {
        public SettingPageHistory() : base(Properties.Resources.SettingPageHistory)
        {
            this.Children = new List<SettingPage>
            {
                new SettingPageHistoryGeneral(),
            };
        }
    }

    public class SettingPageHistoryGeneral : SettingPage
    {
        public SettingPageHistoryGeneral() : base(Properties.Resources.SettingPageHistoryGeneral)
        {
            this.Items = new List<SettingItem>
            {
                new SettingItemSection(Properties.Resources.SettingPageHistoryGeneralGeneral,
                    new SettingItemIndexValue<int>(PropertyMemberElement.Create(BookHub.Current, nameof(BookHub.HistoryEntryPageCount)), new HistoryEntryPageCount(), true),
                    new SettingItemProperty(PropertyMemberElement.Create(BookHub.Current, nameof(BookHub.IsInnerArchiveHistoryEnabled))),
                    new SettingItemProperty(PropertyMemberElement.Create(BookHub.Current, nameof(BookHub.IsUncHistoryEnabled)))),

                new SettingItemSection(Properties.Resources.SettingPageHistoryGeneralLimit, Properties.Resources.SettingPageHistoryGeneralLimitTips,
                    new SettingItemIndexValue<int>(PropertyMemberElement.Create(BookHistory.Current, nameof(BookHistory.LimitSize)), new HistoryLimitSize(), false),
                    new SettingItemIndexValue<TimeSpan>(PropertyMemberElement.Create(BookHistory.Current, nameof(BookHistory.LimitSpan)), new HistoryLimitSpan(), false)),

                new SettingItemSection(Properties.Resources.SettingPageHistoryGeneralDelete,
                    new SettingItemGroup(
                        new SettingItemButton(Properties.Resources.SettingPageHistoryGeneralDeleteButton, RemoveHistory) { IsContentOnly = true })),

                new SettingItemSection(Properties.Resources.SettingPageHistoryGeneralFolderList,
                    new SettingItemProperty(PropertyMemberElement.Create(BookHistory.Current, nameof(BookHistory.IsKeepFolderStatus)))),

#if false
                new SettingItemSection(Properties.Resources.SettingPageHistoryGeneralAdvance,
                    new SettingItemProperty(PropertyMemberElement.Create(App.Current, nameof(App.IsSaveHistory)))),
#endif
            };
        }

        #region Commands

        /// <summary>
        /// RemoveHistory command.
        /// </summary>
        public RelayCommand<UIElement> RemoveHistory
        {
            get { return _RemoveHistory = _RemoveHistory ?? new RelayCommand<UIElement>(RemoveHistory_Executed); }
        }

        //
        private RelayCommand<UIElement> _RemoveHistory;

        //
        private void RemoveHistory_Executed(UIElement element)
        {
            BookHistory.Current.Clear();

            var dialog = new MessageDialog("", Properties.Resources.DialogHistoryDeletedTitle);
            if (element != null)
            {
                dialog.Owner = Window.GetWindow(element);
            }
            dialog.ShowDialog();
        }

        #endregion

        #region IndexValues

        #region IndexValue

        /// <summary>
        /// 履歴登録開始テーブル
        /// </summary>
        public class HistoryEntryPageCount : IndexIntValue
        {
            private static List<int> _values = new List<int>
            {
                0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 20, 50, 100,
            };

            public HistoryEntryPageCount() : base(_values)
            {
                IsValueSyncIndex = false;
            }

            public HistoryEntryPageCount(int value) : base(_values)
            {
                IsValueSyncIndex = false;
                Value = value;
            }

            public override string ValueString => $"{Value} {Properties.Resources.WordPage}";
        }

        #endregion

        /// <summary>
        /// 履歴サイズテーブル
        /// </summary>
        public class HistoryLimitSize : IndexIntValue
        {
            private static List<int> _values = new List<int>
            {
                0, 1, 10, 20, 50, 100, 200, 500, 1000, -1
            };

            public HistoryLimitSize() : base(_values)
            {
            }

            public HistoryLimitSize(int value) : base(_values)
            {
                Value = value;
            }

            public override string ValueString => Value == -1 ? Properties.Resources.WordNoLimit : Value.ToString();
        }

        /// <summary>
        /// 履歴期限テーブル
        /// </summary>
        public class HistoryLimitSpan : IndexTimeSpanValue
        {
            private static List<TimeSpan> _values = new List<TimeSpan>() {
                TimeSpan.FromDays(1),
                TimeSpan.FromDays(2),
                TimeSpan.FromDays(3),
                TimeSpan.FromDays(7),
                TimeSpan.FromDays(15),
                TimeSpan.FromDays(30),
                TimeSpan.FromDays(100),
                default(TimeSpan),
            };

            public HistoryLimitSpan() : base(_values)
            {
            }

            public HistoryLimitSpan(TimeSpan value) : base(_values)
            {
                Value = value;
            }

            public override string ValueString => Value == default(TimeSpan) ? Properties.Resources.WordNoLimit : string.Format(Properties.Resources.WordDaysAgo, Value.Days);
        }

        #endregion
    }
}
