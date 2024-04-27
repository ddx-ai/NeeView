﻿using NeeLaboratory.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NeeView
{
    public class PageCommandResource
    {
        private readonly IToolTipService? _toolTipService;

        public PageCommandResource()
        {
        }

        public PageCommandResource(IToolTipService? toolTipService)
        {
            _toolTipService = toolTipService;
        }


        public bool IsToolTipEnabled
        {
            get { return _toolTipService is not null && _toolTipService.IsToolTipEnabled; }
            set { if (_toolTipService != null) _toolTipService.IsToolTipEnabled = value; }
        }


        public CommandBinding CreateCommandBinding(RoutedCommand command, string? key = null)
        {
            key = key ?? command.Name;
            return key switch
            {
                "OpenCommand" => new CommandBinding(command, Open_Exec, Open_CanExec),
                "OpenBookCommand" => new CommandBinding(command, OpenBook_Exec, OpenBook_CanExec),
                "OpenExplorerCommand" => new CommandBinding(command, OpenExplorer_Executed, OpenExplorer_CanExecute),
                "OpenExternalAppCommand" => new CommandBinding(command, OpenExternalApp_Executed, OpenExternalApp_CanExecute),
                "CopyCommand" => new CommandBinding(command, Copy_Exec, Copy_CanExec),
                "CopyToFolderCommand" => new CommandBinding(command, CopyToFolder_Execute, CopyToFolder_CanExecute),
                "MoveToFolderCommand" => new CommandBinding(command, MoveToFolder_Execute, MoveToFolder_CanExecute),
                "RemoveCommand" => new CommandBinding(command, Remove_Exec, Remove_CanExec),
                "RenameCommand" => new CommandBinding(command, Rename_Execute, Rename_CanExecute),
                "OpenDestinationFolderCommand" => new CommandBinding(command, OpenDestinationFolderDialog_Execute),
                "OpenExternalAppDialogCommand" => new CommandBinding(command, OpenExternalAppDialog_Execute),
                "PlaylistMarkCommand" => new CommandBinding(command, PlaylistMark_Execute, PlaylistMark_CanExecute),
                _ => throw new ArgumentOutOfRangeException(nameof(key)),
            };
        }

        protected virtual Page? GetSelectedPage(object sender)
        {
            var page = (sender as ListBox)?.SelectedItem as Page;
            if (page is null) return null;
            return page.PageType != PageType.Empty ? page : null;
        }

        protected virtual List<Page>? GetSelectedPages(object sender)
        {
            return (sender as ListBox)?.SelectedItems?
                .Cast<Page>()
                .Where(e => e.PageType != PageType.Empty)
                .WhereNotNull()
                .ToList();
        }

        private ListBox? GetListBox(object sender)
        {
            return sender as ListBox;
        }


        /// <summary>
        /// ページを開く
        /// </summary>
        public void Open_CanExec(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (sender as ListBox)?.SelectedItem is Page;
        }

        public void Open_Exec(object sender, ExecutedRoutedEventArgs e)
        {
            if ((sender as ListBox)?.SelectedItem is not Page page) return;

            Jump(page);
        }

        private void Jump(Page page)
        {
            BookOperation.Current.JumpPage(this, page);
        }


        /// <summary>
        /// ブックを開く
        /// </summary>
        public void OpenBook_CanExec(object sender, CanExecuteRoutedEventArgs e)
        {
            var page = GetSelectedPage(sender);
            e.CanExecute = page != null && page.PageType == PageType.Folder;
        }

        public void OpenBook_Exec(object sender, ExecutedRoutedEventArgs e)
        {
            var page = GetSelectedPage(sender);
            if (page == null) return;

            if (page.PageType == PageType.Folder)
            {
                BookHub.Current.RequestLoad(this, page.ArchiveEntry.SystemPath, null, BookLoadOption.IsBook | BookLoadOption.SkipSamePlace, true);
            }
        }

        /// <summary>
        /// エクスプローラーで開くコマンド実行
        /// </summary>
        public void OpenExplorer_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var item = GetSelectedPage(sender);
            e.CanExecute = item != null;
        }

        public void OpenExplorer_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var item = GetSelectedPage(sender);
            if (item != null)
            {
                string? path;
                if (FileIO.ExistsPath(item.EntryFullName))
                {
                    path = item.EntryFullName;
                }
                else
                {
                    path = ArchiveEntryUtility.GetExistEntryName(item.SystemPath);
                }
                ExternalProcess.OpenWithExplorer("/select,\"" + path + "\"");
            }
        }

        /// <summary>
        /// 外部アプリで開く
        /// </summary>
        public void OpenExternalApp_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = OpenExternalApp_CanExecute(sender);
        }

        public bool OpenExternalApp_CanExecute(object sender)
        {
            var items = GetSelectedPages(sender);
            return items != null && items.Any() && CanCopyToFolder(items);
        }

        public async void OpenExternalApp_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is not ExternalApp externalApp) return;

            var items = GetSelectedPages(sender);
            if (items != null && items.Any())
            {
                await externalApp.Execute(items, CancellationToken.None);
            }
        }

        /// <summary>
        /// クリップボードにコピー
        /// </summary>
        public void Copy_CanExec(object sender, CanExecuteRoutedEventArgs e)
        {
            var items = GetSelectedPages(sender);
            e.CanExecute = items != null && items.Any() && CanCopyToFolder(items);
        }

        public async void Copy_Exec(object sender, ExecutedRoutedEventArgs e)
        {
            var items = GetSelectedPages(sender);

            if (items != null && items.Count > 0)
            {
                try
                {
                    App.Current.MainWindow.Cursor = Cursors.Wait;
                    await CopyAsync(items, CancellationToken.None);
                }
                finally
                {
                    App.Current.MainWindow.Cursor = null;
                }
            }

            e.Handled = true;
        }

        private static async Task CopyAsync(List<Page> pages, CancellationToken token)
        {
            await ClipboardUtility.CopyAsync(pages, token);
        }

        private static bool CanCopyToFolder(IEnumerable<Page> pages)
        {
            return PageUtility.CanCreateRealizedFilePathList(pages);
        }

        private static async Task CopyToFolderAsync(IEnumerable<Page> pages, string destDirPath, CancellationToken token)
        {
            var paths = await PageUtility.CreateRealizedFilePathListAsync(pages, token);
            FileIO.CopyToFolder(paths, destDirPath);
        }


        /// <summary>
        /// フォルダーにコピーコマンド用
        /// </summary>
        public void CopyToFolder_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CopyToFolder_CanExecute(sender);
        }

        public bool CopyToFolder_CanExecute(object sender)
        {
            var items = GetSelectedPages(sender);
            return items != null && items.Any() && CanCopyToFolder(items);
        }

        public async void CopyToFolder_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is not DestinationFolder folder) return;

            try
            {
                if (!Directory.Exists(folder.Path))
                {
                    throw new DirectoryNotFoundException();
                }

                var items = GetSelectedPages(sender);
                if (items != null && items.Any())
                {
                    ////Debug.WriteLine($"CopyToFolder: to {folder.Path}");
                    await CopyToFolderAsync(items, folder.Path, CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ToastService.Current.Show(new Toast(ex.Message, Properties.TextResources.GetString("Bookshelf.CopyToFolderFailed"), ToastIcon.Error));
            }
            finally
            {
                e.Handled = true;
            }
        }


        /// <summary>
        /// フォルダーに移動コマンド用
        /// </summary>
        public void MoveToFolder_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = MoveToFolder_CanExecute(sender);
        }

        public bool MoveToFolder_CanExecute(object sender)
        {
            var items = GetSelectedPages(sender);
            return Config.Current.System.IsFileWriteAccessEnabled && items != null && items.Any() && CanMoveToFolder(items);
        }

        public void MoveToFolder_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is not DestinationFolder folder) return;

            try
            {
                if (!Directory.Exists(folder.Path))
                {
                    throw new DirectoryNotFoundException();
                }

                var items = GetSelectedPages(sender);
                if (items != null && items.Any())
                {
                    ////Debug.WriteLine($"MoveToFolder: to {folder.Path}");
                    MoveToFolder(items, folder.Path);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ToastService.Current.Show(new Toast(ex.Message, Properties.TextResources.GetString("PageList.Message.MoveToFolderFailed"), ToastIcon.Error));
            }
            finally
            {
                e.Handled = true;
            }
        }

        protected virtual bool CanMoveToFolder(IEnumerable<Page> pages)
        {
            return pages.All(e => e.ArchiveEntry.IsFileSystem && e.ArchiveEntry.Archiver is not PlaylistArchive);
        }

        private static void MoveToFolder(IEnumerable<Page> pages, string destDirPath)
        {
            var movePages = pages.Where(e => e.ArchiveEntry.IsFileSystem).ToList();
            var paths = movePages.Select(e => e.GetFilePlace()).WhereNotNull().ToList();
            FileIO.MoveToFolder(paths, destDirPath);

            // 移動後のブックページ整合性処理
            // TODO: しっかり実装するならページのファイルシステムの監視が必要になる。ファイルの追加削除が自動的にページに反映するように。
            BookOperation.Current.BookControl.ValidateRemoveFile(movePages);
        }

        #region Remove

        /// <summary>
        /// 削除
        /// </summary>
        public void Remove_CanExec(object sender, CanExecuteRoutedEventArgs e)
        {
            var items = GetSelectedPages(sender);
            if (items is null || items.Count <= 0)
            {
                e.CanExecute = false;
            }
            else
            {
                e.CanExecute = CanRemove(items);
            }
        }

        public async void Remove_Exec(object sender, ExecutedRoutedEventArgs e)
        {
            var items = GetSelectedPages(sender);
            if (items is null || items.Count <= 0)
            {
                return;
            }

            await RemoveAsync(items);
            e.Handled = true;
        }

        private static bool CanRemove(List<Page> pages)
        {
            return BookOperation.Current.Control.CanDeleteFile(pages);
        }

        private static async Task RemoveAsync(List<Page> pages)
        {
            await BookOperation.Current.Control.DeleteFileAsync(pages);
        }

        #endregion Remove

        #region Rename

        public void Rename_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var item = GetSelectedPage(sender);
            if (item is null) return;

            e.CanExecute = Config.Current.System.IsFileWriteAccessEnabled && item.CanRename();
        }

        public async void Rename_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            var listBox = GetListBox(sender);
            if (listBox is null) return;

            var item = GetSelectedPage(sender);
            if (item is null) return;

            var renamer = new PageListItemRenamer(listBox, _toolTipService);
            renamer.SelectedItemChanged += (s, e) => BookOperation.Current.JumpPage(this, listBox.SelectedItem as Page);
            renamer.ArchiveChanged += (s, e) => BookOperation.Current.BookControl.ReLoad();
            await renamer.RenameAsync(item);
        }

        #endregion Rename

        public void OpenDestinationFolderDialog_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            var listBox = sender as ListBox;
            DestinationFolderDialog.ShowDialog(Window.GetWindow(listBox));
        }

        public void OpenExternalAppDialog_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            var listBox = sender as ListBox;
            ExternalAppDialog.ShowDialog(Window.GetWindow(listBox));
        }


        public void PlaylistMark_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var book = BookOperation.Current.Book;
            if (book is null) return;

            var items = GetSelectedPages(sender);
            var bookPlaylist = new BookPlaylist(book, PlaylistHub.Current.Playlist);

            if (items != null && items.Count > 0)
            {
                e.CanExecute = items.All(x => bookPlaylist.IsEnabled(x));
            }
            else
            {
                e.CanExecute = false;
            }
        }

        public void PlaylistMark_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            var book = BookOperation.Current.Book;
            if (book is null) return;

            var pages = GetSelectedPages(sender);
            if (pages is null) return;

            var bookPlaylist = new BookPlaylist(book, PlaylistHub.Current.Playlist);

            if (PlaylistMark_IsChecked(sender))
            {
                bookPlaylist.Remove(pages);
            }
            else
            {
                bookPlaylist.Add(pages);
            }
        }

        public bool PlaylistMark_IsChecked(object sender)
        {
            var book = BookOperation.Current.Book;
            if (book is null) return false;

            var page = GetSelectedPage(sender);
            if (page is null) return false;

            var bookPlaylist = new BookPlaylist(book, PlaylistHub.Current.Playlist);
            return bookPlaylist.Find(page) != null;
        }
    }


}
