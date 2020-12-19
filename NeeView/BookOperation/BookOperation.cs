﻿using NeeLaboratory.ComponentModel;
using NeeView.Collections;
using NeeView.Collections.Generic;
using NeeView.Properties;
using NeeView.Windows.Property;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NeeView
{
    /// <summary>
    /// 本の操作
    /// </summary>
    public class BookOperation : BindableBase
    {
        // System Object
        static BookOperation() => Current = new BookOperation();
        public static BookOperation Current { get; }

        #region Fields

        private bool _isEnabled;
        private Book _book;
        private ObservableCollection<Page> _pageList;
        private ExportImageProceduralDialog _exportImageProceduralDialog;
        private int _pageTerminating;

        #endregion

        #region Constructors

        private BookOperation()
        {
            BookHub.Current.BookChanging += BookHub_BookChanging;
            BookHub.Current.BookChanged += BookHub_BookChanged;

            PagemarkCollection.Current.PagemarkChanged += PagemarkCollection_PagemarkChanged;
        }

        #endregion

        #region Events

        // ブックが変更される
        public event EventHandler<BookChangingEventArgs> BookChanging;

        // ブックが変更された
        public event EventHandler<BookChangedEventArgs> BookChanged;

        // ページが変更された
        public event EventHandler<ViewContentSourceCollectionChangedEventArgs> ViewContentsChanged;

        // ページがソートされた
        public event EventHandler PagesSorted;

        // ページリストが変更された
        public event EventHandler PageListChanged;

        // ページが削除された
        public event EventHandler<PageRemovedEventArgs> PageRemoved;

        #endregion

        #region Properties

        /// <summary>
        /// 操作の有効設定。ロード中は機能を無効にするために使用
        /// </summary>
        public bool IsEnabled
        {
            get { return _isEnabled; }
            set { if (_isEnabled != value) { _isEnabled = value; RaisePropertyChanged(); } }
        }

        public Book Book
        {
            get { return _book; }
            set
            {
                if (SetProperty(ref _book, value))
                {
                    RaisePropertyChanged(nameof(Address));
                    RaisePropertyChanged(nameof(IsValid));
                    RaisePropertyChanged(nameof(IsBusy));
                }
            }
        }


        public string Address => Book?.Address;

        public bool IsValid => Book != null;

        public bool IsBusy
        {
            get { return Book != null ? Book.Viewer.IsBusy : false; }
        }

        public ObservableCollection<Page> PageList
        {
            get { return _pageList; }
        }

        #endregion

        #region Methods


        private void BookHub_BookChanging(object sender, BookChangingEventArgs e)
        {
            // ブック操作無効
            IsEnabled = false;

            BookChanging?.Invoke(sender, e);
        }

        /// <summary>
        /// 本の更新
        /// </summary>
        private void BookHub_BookChanged(object sender, BookChangedEventArgs e)
        {
            this.Book = BookHub.Current.Book;

            if (this.Book != null)
            {
                this.Book.Pages.PagesSorted += Book_PagesSorted;
                this.Book.Pages.PageRemoved += Book_PageRemoved;
                this.Book.Viewer.ViewContentsChanged += Book_ViewContentsChanged;
                this.Book.Viewer.PageTerminated += Book_PageTerminated;
                this.Book.Viewer.AddPropertyChanged(nameof(BookPageViewer.IsBusy), (s, e_) => RaisePropertyChanged(nameof(IsBusy)));
            }

            //
            RaisePropertyChanged(nameof(IsBookmark));

            // マーカー復元
            // TODO: PageMarkersのしごと？
            UpdatePagemark();

            // ページリスト更新
            UpdatePageList(false);

            // ブック操作有効
            IsEnabled = true;

            // ページリスト更新通知
            PageListChanged?.Invoke(this, null);

            BookChanged?.Invoke(sender, e);
        }

        //
        private void Book_PagesSorted(object sender, EventArgs e)
        {
            AppDispatcher.Invoke(() =>
            {
                UpdatePageList(true);
            });

            PagesSorted?.Invoke(this, e);
        }

        //
        private void Book_ViewContentsChanged(object sender, ViewContentSourceCollectionChangedEventArgs e)
        {
            if (!IsEnabled) return;

            AppDispatcher.Invoke(() =>
            {
                RaisePropertyChanged(nameof(IsPagemark));
                ViewContentsChanged?.Invoke(sender, e);
            });
        }


        // ページリスト更新
        // TODO: クリアしてもサムネイルのListBoxは項目をキャッシュしてしまうので、なんとかせよ
        // サムネイル用はそれに特化したパーツのみ提供する？
        // いや、ListBoxを独立させ、それ自体を作り直す方向で？んー？
        // 問い合わせがいいな。
        // 問い合わせといえば、BitmapImageでOutOfMemoryが取得できない問題も。
        public void UpdatePageList(bool raisePageListChanged)
        {
            var pages = this.Book?.Pages;
            var pageList = pages != null ? new ObservableCollection<Page>(pages) : null;

            if (SetProperty(ref _pageList, pageList, nameof(PageList)))
            {
                if (raisePageListChanged)
                {
                    PageListChanged?.Invoke(this, null);
                }
            }

            RaisePropertyChanged(nameof(IsPagemark));
        }


        // 現在ページ番号取得
        public int GetPageIndex()
        {
            return this.Book == null ? 0 : this.Book.Viewer.DisplayIndex; // GetPosition().Index;
        }

        // 現在ページ番号を設定し、表示を切り替える (先読み無し)
        public void RequestPageIndex(object sender, int index)
        {
            this.Book?.Control.RequestSetPosition(sender, new PagePosition(index, 0), 1);
        }

        /// <summary>
        /// 最大ページ番号取得
        /// </summary>
        /// <returns></returns>
        public int GetMaxPageIndex()
        {
            var count = this.Book == null ? 0 : this.Book.Pages.Count - 1;
            if (count < 0) count = 0;
            return count;
        }

        /// <summary>
        /// ページ数取得
        /// </summary>
        /// <returns></returns>
        public int GetPageCount()
        {
            return this.Book == null ? 0 : this.Book.Pages.Count;
        }

        #endregion

        #region BookCommand : ページ削除

        // 現在表示しているページのファイル削除可能？
        public bool CanDeleteFile()
        {
            return CanDeleteFile(Book?.Viewer.GetViewPage());
        }

        // 現在表示しているページのファイルを削除する
        public async Task DeleteFileAsync()
        {
            await DeleteFileAsync(Book?.Viewer.GetViewPage());
        }

        // 指定ページのファル削除可能？
        public bool CanDeleteFile(Page page)
        {
            return Config.Current.System.IsFileWriteAccessEnabled && FileIO.Current.CanRemovePage(page);
        }

        // 指定ページのファルを削除する
        public async Task DeleteFileAsync(Page page)
        {
            if (CanDeleteFile(page))
            {
                var isSuccess = await FileIO.Current.RemovePageAsync(page);
                if (isSuccess)
                {
                    Book.Control.RequestRemove(this, page);
                }
            }
        }

        // 指定ページのファイルを削除する
        public async Task DeleteFileAsync(List<Page> pages)
        {
            var removes = pages.Where(e => CanDeleteFile(e)).ToList();
            if (removes.Any())
            {
                if (removes.Count == 1)
                {
                    await DeleteFileAsync(removes.First());
                    return;
                }

                await FileIO.Current.RemovePageAsync(removes);
                ValidateRemoveFile(removes);
            }
        }

        // 消えたファイルのページを削除
        public void ValidateRemoveFile(IEnumerable<Page> pages)
        {
            Book.Control.RequestRemove(this, pages.Where(e => FileIO.Current.IsPageRemoved(e)).ToList());
        }


        #endregion

        #region BookCommand : ブック削除

        // 現在表示しているブックの削除可能？
        public bool CanDeleteBook()
        {
            return Config.Current.System.IsFileWriteAccessEnabled && Book != null && (Book.LoadOption & BookLoadOption.Undeliteable) == 0 && (File.Exists(Book.SourceAddress) || Directory.Exists(Book.SourceAddress));
        }

        // 現在表示しているブックを削除する
        public async void DeleteBook()
        {
            if (CanDeleteBook())
            {
                var item = BookshelfFolderList.Current.FindFolderItem(Book.SourceAddress);
                if (item != null)
                {
                    await BookshelfFolderList.Current.RemoveAsync(item);
                }
                else
                {
                    await FileIO.Current.RemoveFileAsync(Book.SourceAddress, Resources.DialogFileDeleteBookTitle, null);
                }
            }
        }

        #endregion

        #region BookCommand : ページ出力

        // ファイルの場所を開くことが可能？
        public bool CanOpenFilePlace()
        {
            return Book?.Viewer.GetViewPage() != null;
        }

        // ファイルの場所を開く
        public void OpenFilePlace()
        {
            if (CanOpenFilePlace())
            {
                string place = Book.Viewer.GetViewPage()?.GetFolderOpenPlace();
                if (place != null)
                {
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + place + "\"");
                }
            }
        }


        // 外部アプリで開く
        public void OpenApplication(OpenExternalAppCommandParameter parameter)
        {
            if (CanOpenFilePlace())
            {
                var external = new ExternalAppUtility();
                try
                {
                    external.Call(Book?.Viewer.GetViewPages(), parameter, CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    new MessageDialog(ex.Message, Properties.Resources.DialogOpenApplicationErrorTitle).ShowDialog();
                }
            }
        }


        // クリップボードにコピー
        public void CopyToClipboard(CopyFileCommandParameter parameter)
        {
            if (CanOpenFilePlace())
            {
                try
                {
                    ClipboardUtility.Copy(Book?.Viewer.GetViewPages(), parameter);
                }
                catch (Exception e)
                {
                    new MessageDialog($"{Resources.WordCause}: {e.Message}", Resources.DialogCopyErrorTitle).ShowDialog();
                }
            }
        }

        /// <summary>
        /// ファイル保存可否
        /// </summary>
        /// <returns></returns>
        public bool CanExport()
        {
            var pages = Book?.Viewer.GetViewPages();
            if (pages == null || pages.Count == 0) return false;

            var imageSource = pages[0].GetContentImageSource();
            if (imageSource == null) return false;

            return true;
        }


        // ファイルに保存する (ダイアログ)
        // TODO: OutOfMemory対策
        public void ExportDialog(ExportImageAsCommandParameter parameter)
        {
            if (CanExport())
            {
                try
                {
                    _exportImageProceduralDialog = _exportImageProceduralDialog ?? new ExportImageProceduralDialog();
                    _exportImageProceduralDialog.Show(parameter);
                }
                catch (Exception e)
                {
                    new MessageDialog($"{Resources.DialogImageExportError}\n{Resources.WordCause}: {e.Message}", Resources.DialogImageExportErrorTitle).ShowDialog();
                    return;
                }
            }
        }

        // ファイルに保存する
        public void Export(ExportImageCommandParameter parameter)
        {
            if (CanExport())
            {
                try
                {
                    var process = new ExportImageProcedure();
                    process.Execute(parameter);
                }
                catch (Exception e)
                {
                    new MessageDialog($"{Resources.DialogImageExportError}\n{Resources.WordCause}: {e.Message}", Resources.DialogImageExportErrorTitle).ShowDialog();
                    return;
                }
            }
        }

        #endregion

        #region BookCommand : ページ操作

        // ページ終端を超えて移動しようとするときの処理
        private void Book_PageTerminated(object sender, PageTerminatedEventArgs e)
        {
            if (_pageTerminating > 0) return;

            // TODO ここでSlideShowを参照しているが、引数で渡すべきでは？
            if (SlideShow.Current.IsPlayingSlideShow && Config.Current.SlideShow.IsSlideShowByLoop)
            {
                FirstPage(sender);
            }
            else
            {
                switch (Config.Current.Book.PageEndAction)
                {
                    case PageEndAction.Loop:
                        PageEndAction_Loop(sender, e);
                        break;

                    case PageEndAction.NextBook:
                        PageEndAction_NextBook(sender, e);
                        break;

                    case PageEndAction.Dialog:
                        PageEndAction_Dialog(sender, e);
                        break;

                    default:
                        PageEndAction_None(sender, e, true);
                        break;
                }
            }
        }

        private void PageEndAction_Loop(object sender, PageTerminatedEventArgs e)
        {
            if (e.Direction < 0)
            {
                LastPage(sender);
            }
            else
            {
                FirstPage(sender);
            }
            if (Config.Current.Book.IsNotifyPageLoop)
            {
                InfoMessage.Current.SetMessage(InfoMessageType.Notify, Properties.Resources.NotifyBookOperationPageLoop);
            }
        }

        private void PageEndAction_NextBook(object sender, PageTerminatedEventArgs e)
        {
            AppDispatcher.Invoke(async () =>
            {
                if (e.Direction < 0)
                {
                    await BookshelfFolderList.Current.PrevFolder(BookLoadOption.LastPage);
                }
                else
                {
                    await BookshelfFolderList.Current.NextFolder(BookLoadOption.FirstPage);
                }
            });
        }

        private void PageEndAction_None(object sender, PageTerminatedEventArgs e, bool notify)
        {
            if (SlideShow.Current.IsPlayingSlideShow)
            {
                // スライドショー解除
                SlideShow.Current.IsPlayingSlideShow = false;
            }

            // 通知。本の場合のみ処理。メディアでは不要
            else if (notify && this.Book != null && !this.Book.IsMedia)
            {
                if (e.Direction < 0)
                {
                    SoundPlayerService.Current.PlaySeCannotMove();
                    InfoMessage.Current.SetMessage(InfoMessageType.Notify, Properties.Resources.NotifyFirstPage);
                }
                else
                {
                    SoundPlayerService.Current.PlaySeCannotMove();
                    InfoMessage.Current.SetMessage(InfoMessageType.Notify, Properties.Resources.NotifyLastPage);
                }
            }
        }

        private void PageEndAction_Dialog(object sender, PageTerminatedEventArgs e)
        {
            Interlocked.Increment(ref _pageTerminating);

            AppDispatcher.BeginInvoke(() =>
            {
                try
                {
                    PageEndAction_DialogCore(sender, e);
                }
                finally
                {
                    Interlocked.Decrement(ref _pageTerminating);
                }
            });
        }

        private void PageEndAction_DialogCore(object sender, PageTerminatedEventArgs e)
        {
            var title = (e.Direction < 0) ? Resources.NotifyFirstPage : Resources.NotifyLastPage;
            var dialog = new MessageDialog(Resources.DialogPageEnd, title);
            var nextCommand = new UICommand(Properties.Resources.PageEndAction_NextBook);
            var loopCommand = new UICommand(Properties.Resources.PageEndAction_Loop);
            var noneCommand = new UICommand(Properties.Resources.PageEndAction_None);
            dialog.Commands.Add(nextCommand);
            dialog.Commands.Add(loopCommand);
            dialog.Commands.Add(noneCommand);
            var result = dialog.ShowDialog(App.Current.MainWindow);

            if (result == nextCommand)
            {
                PageEndAction_NextBook(sender, e);
            }
            else if (result == loopCommand)
            {
                PageEndAction_Loop(sender, e);
            }
            else
            {
                PageEndAction_None(sender, e, false);
            }
        }


        // ページ削除時の処理
        private void Book_PageRemoved(object sender, PageRemovedEventArgs e)
        {
            // ページマーカーから削除
            foreach (var page in e.Pages)
            {
                RemovePagemark(this.Book.Address, page.EntryFullName);
            }

            UpdatePageList(true);
            PageRemoved?.Invoke(sender, e);
        }

        // ページ移動量をメディアの時間移動量に変換して移動
        private void MoveMediaPage(object sender, int delta)
        {
            if (MediaPlayerOperator.Current == null) return;

            var isTerminated = MediaPlayerOperator.Current.AddPosition(TimeSpan.FromSeconds(delta * Config.Current.Archive.Media.PageSeconds));

            if (isTerminated)
            {
                this.Book?.Viewer.RaisePageTerminatedEvent(sender, delta < 0 ? -1 : 1);
            }
        }

        // 前のページに移動
        public void PrevPage(object sender)
        {
            if (this.Book == null) return;

            if (this.Book.IsMedia)
            {
                MoveMediaPage(sender, -1);
            }
            else
            {
                this.Book.Control.PrevPage(sender, 0);
            }
        }

        // 次のページに移動
        public void NextPage(object sender)
        {
            if (this.Book == null) return;

            if (this.Book.IsMedia)
            {
                MoveMediaPage(sender, +1);
            }
            else
            {
                this.Book.Control.NextPage(sender, 0);
            }
        }

        // 1ページ前に移動
        public void PrevOnePage(object sender)
        {
            if (this.Book == null) return;

            if (this.Book.IsMedia)
            {
                MoveMediaPage(sender, -1);
            }
            else
            {
                this.Book.Control.PrevPage(sender, 1);
            }
        }

        // 1ページ後に移動
        public void NextOnePage(object sender)
        {
            if (this.Book == null) return;

            if (this.Book.IsMedia)
            {
                MoveMediaPage(sender, +1);
            }
            else
            {
                this.Book?.Control.NextPage(sender, 1);
            }
        }

        // 指定ページ数前に移動
        public void PrevSizePage(object sender, int size)
        {
            if (this.Book == null) return;

            if (this.Book.IsMedia)
            {
                MoveMediaPage(sender, -size);
            }
            else
            {
                this.Book.Control.PrevPage(sender, size);
            }
        }

        // 指定ページ数後に移動
        public void NextSizePage(object sender, int size)
        {
            if (this.Book == null) return;

            if (this.Book.IsMedia)
            {
                MoveMediaPage(sender, +size);
            }
            else
            {
                this.Book.Control.NextPage(sender, size);
            }
        }


        // 最初のページに移動
        public void FirstPage(object sender)
        {
            if (this.Book == null) return;

            if (this.Book.IsMedia)
            {
                MediaPlayerOperator.Current?.SetPositionFirst();
            }
            else
            {
                this.Book.Control.FirstPage(sender);
            }
        }

        // 最後のページに移動
        public void LastPage(object sender)
        {
            if (this.Book == null) return;

            if (this.Book.IsMedia)
            {
                MediaPlayerOperator.Current?.SetPositionLast();
            }
            else
            {
                this.Book.Control.LastPage(sender);
            }
        }


        // 前のフォルダーに移動
        public void PrevFolderPage(object sender, bool isShowMessage)
        {
            if (this.Book == null) return;

            if (this.Book.IsMedia)
            {
            }
            else
            {
                var index = this.Book.Control.PrevFolderPage(sender);
                ShowMoveFolderPageMessage(index, Properties.Resources.NotifyFirstFolderPage, isShowMessage);
            }
        }

        // 次のフォルダーに移動
        public void NextFolderPage(object sender, bool isShowMessage)
        {
            if (this.Book == null) return;

            if (this.Book.IsMedia)
            {
            }
            else
            {
                var index = this.Book.Control.NextFolderPage(sender);
                ShowMoveFolderPageMessage(index, Properties.Resources.NotifyLastFolderPage, isShowMessage);
            }
        }

        private void ShowMoveFolderPageMessage(int index, string termianteMessage, bool isShowMessage)
        {
            if (index < 0)
            {
                InfoMessage.Current.SetMessage(InfoMessageType.Notify, termianteMessage);
            }
            else if (isShowMessage)
            {
                var directory = this.Book.Pages[index].GetSmartDirectoryName();
                if (string.IsNullOrEmpty(directory))
                {
                    directory = "(Root)";
                }
                InfoMessage.Current.SetMessage(InfoMessageType.Notify, directory);
            }
        }


        // ページを指定して移動
        public void JumpPage(object sender, int number)
        {
            if (this.Book == null || this.Book.IsMedia) return;

            var page = this.Book.Pages.GetPage(number - 1);
            this.Book.Control.JumpPage(sender, page);
        }

        // ページ名を指定して移動
        public void JumpPage(object sender, string name)
        {
            if (this.Book == null || this.Book.IsMedia) return;

            var page = this.Book.Pages.GetPage(name);
            this.Book.Control.JumpPage(sender, page);
        }

        // ページを指定して移動
        public void JumpPageAs(object sender)
        {
            if (this.Book == null || this.Book.IsMedia) return;

            var dialogModel = new PageSelecteDialogModel()
            {
                Value = this.Book.Viewer.GetViewPageIndex() + 1,
                Min = 1,
                Max = this.Book.Pages.Count
            };

            var dialog = new PageSelectDialog(dialogModel);
            dialog.Owner = MainWindow.Current;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var result = dialog.ShowDialog();

            if (result == true)
            {
                var page = this.Book.Pages.GetPage(dialogModel.Value - 1);
                this.Book.Control.JumpPage(sender, page);
            }
        }

        // 指定ページに移動
        public void JumpPage(object sender, Page page)
        {
            if (_isEnabled && page != null) this.Book?.Control.JumpPage(sender, page);
        }

        // ランダムページに移動
        public void JumpRandomPage(object sender)
        {
            if (this.Book == null || this.Book.IsMedia) return;
            if (this.Book.Pages.Count <= 1) return;

            var currentIndex = this.Book.Viewer.GetViewPageIndex();

            var random = new Random();
            var index = random.Next(this.Book.Pages.Count - 1);

            if (index == currentIndex)
            {
                index = this.Book.Pages.Count - 1;
            }

            var page = this.Book.Pages.GetPage(index);
            this.Book.Control.JumpPage(sender, page);
        }


        // 動画再生中？
        public bool IsMediaPlaying()
        {
            if (this.Book != null && this.Book.IsMedia)
            {
                return MediaPlayerOperator.Current.IsPlaying;
            }
            else
            {
                return false;
            }
        }

        // 動画再生ON/OFF
        public bool ToggleMediaPlay()
        {
            if (this.Book != null && this.Book.IsMedia)
            {
                if (MediaPlayerOperator.Current.IsPlaying)
                {
                    MediaPlayerOperator.Current.Pause();
                }
                else
                {
                    MediaPlayerOperator.Current.Play();
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        // スライドショー用：次のページへ移動
        public void NextSlide(object sender)
        {
            if (SlideShow.Current.IsPlayingSlideShow)
            {
                NextPage(sender);
            }
        }

        #endregion

        #region BookCommand : ブックマーク

        // ブックマーク登録可能？
        public bool CanBookmark()
        {
            return (Book != null);
        }

        // ブックマーク設定
        public void SetBookmark(bool isBookmark)
        {
            if (CanBookmark())
            {
                var query = new QueryPath(Book.Address);

                if (isBookmark)
                {
                    // ignore temporary directory
                    if (Book.Address.StartsWith(Temporary.Current.TempDirectory))
                    {
                        ToastService.Current.Show(new Toast(Resources.DialogBookmarkError, null, ToastIcon.Error));
                        return;
                    }

                    BookmarkCollectionService.Add(query);
                }
                else
                {
                    BookmarkCollectionService.Remove(query);
                }

                RaisePropertyChanged(nameof(IsBookmark));
            }
        }

        // ブックマーク切り替え
        public void ToggleBookmark()
        {
            if (CanBookmark())
            {
                SetBookmark(!IsBookmark);
            }
        }

        // ブックマーク判定
        public bool IsBookmark
        {
            get
            {
                return BookmarkCollection.Current.Contains(Book?.Address);
            }
        }

        #endregion

        #region BookCommand : ページマーク

        // ページマークにに追加、削除された
        public event EventHandler PagemarkChanged;

        //
        private void PagemarkCollection_PagemarkChanged(object sender, PagemarkCollectionChangedEventArgs e)
        {
            if (!IsValid)
            {
                return;
            }

            var placeQuery = new QueryPath(Address);
            if (placeQuery.Scheme == QueryScheme.Pagemark)
            {
                switch (e.Action)
                {
                    case EntryCollectionChangedAction.Replace:
                    case EntryCollectionChangedAction.Reset:
                        BookHub.Current.RequestUnload(sender, true);
                        break;
                    case EntryCollectionChangedAction.Add:
                        {
                            // ブックに含まれるページが追加されたら再読込 ... シャッフルで再ソートされてしまう問題あり
                            var query = e.Item.CreateQuery(QueryScheme.Pagemark);
                            if (Book.Source.IsRecursiveFolder && placeQuery.Include(query) || query.GetParent().Equals(placeQuery))
                            {
                                Debug.WriteLine($"BookOperation: Add pagemarks: {e.Item.Value.Name}");
                                BookHub.Current.RequestReLoad(sender);
                            }
                        }
                        break;

                    case EntryCollectionChangedAction.Remove:
                        {
                            var parentQuery = e.Parent.CreateQuery(QueryScheme.Pagemark);

                            // 現在のページに存在している場合はその項目を削除
                            // 自身もしくは親フォルダーから削除された場合は本を閉じる
                            // 含まれるフォルダーが削除された場合は再読込
                            var page = Book.Pages.FirstOrDefault(i => i.Entry.Instance is TreeListNode<IPagemarkEntry> node && node == e.Item);
                            if (page != null)
                            {
                                Debug.WriteLine($"BookOperation: Remve pagemark: {e.Item.Value.Name}");
                                Book.Control.RequestRemove(this, page);
                            }
                            else if (PagemarkCollection.Current.FindNode(placeQuery) == null) // 親が削除されていたら見つからない
                            {
                                Debug.WriteLine($"BookOperation: Remove parent pagemark: {e.Item.Value.Name}");
                                BookHub.Current.RequestUnload(sender, true);
                            }
                            else if (e.Item.Value is PagemarkFolder && Book.Source.IsRecursiveFolder && placeQuery.Include(parentQuery))
                            {
                                Debug.WriteLine($"BookOperation: Remove pagemarks: {e.Item.Value.Name}");
                                BookHub.Current.RequestReLoad(sender);
                            }
                        }
                        break;

                    case EntryCollectionChangedAction.Rename:
                        {
                            var parentQuery = e.Parent.CreateQuery(QueryScheme.Pagemark);

                            // 自信か親の名前が変化する場合、閉じる
                            // 含まれるフォルダー名が変化する場合、再読込
                            if (PagemarkCollection.Current.FindNode(placeQuery) == null) // 自身もしくは親の名前が変わっていたら見つからない
                            {
                                Debug.WriteLine($"BookOperation: Rename parent pagemark: {e.Item.Value.Name}");
                                BookHub.Current.RequestUnload(sender, true);
                            }
                            else if (e.Item.Value is PagemarkFolder && Book.Source.IsRecursiveFolder && placeQuery.Include(parentQuery))
                            {
                                Debug.WriteLine($"BookOperation: Rename pagemarks: {e.Item.Value.Name}");
                                BookHub.Current.RequestReLoad(sender);
                            }
                        }
                        break;
                }
            }

            else
            {
                switch (e.Action)
                {
                    case EntryCollectionChangedAction.Replace:
                    case EntryCollectionChangedAction.Reset:
                        UpdatePagemark();
                        break;
                    case EntryCollectionChangedAction.Add:
                    case EntryCollectionChangedAction.Remove:
                        if (e.Item.Value is Pagemark pagemark && pagemark.Path == Address)
                        {
                            UpdatePagemark();
                        }
                        break;
                }
            }
        }

        //
        public bool IsPagemark
        {
            get { return IsMarked(); }
        }

        // 表示ページのマーク判定
        public bool IsMarked()
        {
            return this.Book != null ? this.Book.Marker.IsMarked(this.Book.Viewer.GetViewPage()) : false;
        }

        // ページマーク登録可能？
        public bool CanPagemark()
        {
            return this.Book != null && !this.Book.IsMedia && !this.Book.IsPagemarkFolder;
        }

        // マーカー設定
        public Pagemark SetPagemark(bool isPagemark)
        {
            if (!_isEnabled || this.Book == null || this.Book.IsMedia || this.Book.IsPagemarkFolder) return null;

            var address = Book.Address;
            var entryName = Book.Viewer.GetViewPage()?.EntryFullName;

            if (isPagemark)
            {
                return AddPagemark(address, entryName); ////, page.Length, page.LastWriteTime);
            }
            else
            {
                RemovePagemark(address, entryName);
                return null;
            }
        }

        // マーカー切り替え
        public Pagemark TogglePagemark()
        {
            if (!_isEnabled || this.Book == null || this.Book.IsMedia || this.Book.IsPagemarkFolder) return null;

            var address = Book.Address;
            var entryName = Book.Viewer.GetViewPage()?.EntryFullName;
            var node = PagemarkCollection.Current.FindNode(address, entryName);

            if (node == null)
            {
                return AddPagemark(address, entryName); ////, page.Length, page.LastWriteTime);
            }
            else
            {
                RemovePagemark(address, entryName);
                return null;
            }
        }

        private Pagemark AddPagemark(string place, string entryName)
        {
            if (place == null) return null;
            if (entryName == null) return null;

            // ignore temporary directory
            if (place.StartsWith(Temporary.Current.TempDirectory))
            {
                ToastService.Current.Show(new Toast(Resources.DialogPagemarkError, null, ToastIcon.Error));
                return null;
            }

            var node = PagemarkCollection.Current.FindNode(place, entryName);
            if (node == null)
            {
                // TODO: 登録時にサムネイルキャッシュにも登録
                var pagemark = new Pagemark(place, entryName);
                PagemarkCollection.Current.Add(new TreeListNode<IPagemarkEntry>(pagemark));
                return pagemark;
            }
            else
            {
                return node.Value as Pagemark;
            }
        }

        #region 開発用

        /// <summary>
        /// (開発用) たくさんのページマーク作成
        /// </summary>
        [Conditional("DEBUG")]
        public void Test_MakeManyPagemark()
        {
            if (Book == null) return;
            var place = Book.Address;
            for (int index = 0; index < Book.Pages.Count; index += 100)
            {
                var page = Book.Pages[index];
                AddPagemark(place, page.EntryFullName);
            }
        }

        #endregion

        // マーカー削除
        private bool RemovePagemark(string place, string entryName)
        {
            if (place == null) return false;
            if (entryName == null) return false;

            var node = PagemarkCollection.Current.FindNode(place, entryName);
            if (node != null)
            {
                return PagemarkCollection.Current.Remove(node);
            }
            else
            {
                return false;
            }
        }

        // マーカー表示更新
        public void UpdatePagemark()
        {
            // 本にマーカを設定
            // TODO: これはPagemarkerの仕事？
            this.Book?.Marker.SetMarkers(PagemarkCollection.Current.Collect(this.Book.Address).Select(e => e.EntryName));

            // 表示更新
            PagemarkChanged?.Invoke(this, null);
            RaisePropertyChanged(nameof(IsPagemark));
        }

        //
        public bool CanPrevPagemarkInPlace(MovePagemarkInBookCommandParameter param)
        {
            return (this.Book?.Marker.Markers != null && Current.Book.Marker.Markers.Count > 0) || param.IsIncludeTerminal;
        }

        //
        public bool CanNextPagemarkInPlace(MovePagemarkInBookCommandParameter param)
        {
            return (this.Book?.Marker.Markers != null && Current.Book.Marker.Markers.Count > 0) || param.IsIncludeTerminal;
        }

        // ページマークに移動
        public void PrevPagemarkInPlace(MovePagemarkInBookCommandParameter param)
        {
            if (!_isEnabled || this.Book == null) return;
            var result = this.Book.Control.RequestJumpToMarker(this, -1, param.IsLoop, param.IsIncludeTerminal);
            if (result != null)
            {
                // ページマーク更新
                PagemarkList.Current.Jump(this.Book.Address, result.EntryName);
            }
            else
            {
                InfoMessage.Current.SetMessage(InfoMessageType.Notify, Properties.Resources.NotifyFirstPagemark);
            }
        }

        // ページマークに移動
        public void NextPagemarkInPlace(MovePagemarkInBookCommandParameter param)
        {
            if (!_isEnabled || this.Book == null) return;
            var result = this.Book.Control.RequestJumpToMarker(this, +1, param.IsLoop, param.IsIncludeTerminal);
            if (result != null)
            {
                // ページマーク更新
                PagemarkList.Current.Jump(this.Book.Address, result.EntryName);
            }
            else
            {
                InfoMessage.Current.SetMessage(InfoMessageType.Notify, Properties.Resources.NotifyLastPagemark);
            }
        }

        // ページマークに移動
        public bool JumpPagemarkInPlace(object sender, Pagemark mark)
        {
            if (mark == null) return false;

            if (mark.Path == this.Book?.Address)
            {
                Page page = this.Book.Pages.GetPage(mark.EntryName);
                if (page != null)
                {
                    JumpPage(sender, page);
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region BookCommand : 下位ブックに移動

        public bool CanMoveToChildBook()
        {
            var page = Book?.Viewer.GetViewPage();
            return page != null && page.PageType == PageType.Folder;
        }

        public void MoveToChildBook(object sender)
        {
            var page = Book?.Viewer.GetViewPage();
            if (page != null && page.PageType == PageType.Folder)
            {
                BookHub.Current.RequestLoad(sender, page.Entry.SystemPath, null, BookLoadOption.IsBook | BookLoadOption.SkipSamePlace, true);
            }
        }

        #endregion

        #region Memento
        [DataContract]
        public class Memento : IMemento
        {
            [DataMember]
            public PageEndAction PageEndAction { get; set; }

            [DataMember]
            public ExternalApplicationMemento ExternalApplication { get; set; }

            [DataMember]
            public ClipboardUtilityMemento ClipboardUtility { get; set; }

            [DataMember]
            public bool IsNotifyPageLoop { get; set; }

            public void RestoreConfig(Config config)
            {
                config.Book.PageEndAction = PageEndAction;
                config.Book.IsNotifyPageLoop = IsNotifyPageLoop;

                // NOTE: ExternalApplication と ClipboardUtility は上位で処理されている
            }
        }

        #endregion


        public class ExternalApplicationMemento
        {
            [Obsolete, DataMember]
            public ExternalProgramType ProgramType { get; set; }

            [DataMember]
            public string Command { get; set; }

            [DataMember]
            public string Parameter { get; set; }

            [Obsolete, DataMember]
            public string Protocol { get; set; }

            // 複数ページのときの動作
            [PropertyMember("@ParamExternalMultiPageOption")]
            public MultiPagePolicy MultiPageOption { get; set; }

            // 圧縮ファイルのときの動作
            [DataMember]
            public ArchivePolicy ArchiveOption { get; set; }

            [DataMember]
            public string ArchiveSeparater { get; set; }


            [OnDeserialized]
            private void OnDeserialized(StreamingContext context)
            {
#pragma warning disable CS0612 // 型またはメンバーが旧型式です
                if (ProgramType == ExternalProgramType.Protocol)
                {
                    Command = "";
                    Parameter = Protocol;
                }
#pragma warning restore CS0612 // 型またはメンバーが旧型式です
            }
        }

        [DataContract]
        public class ClipboardUtilityMemento
        {
            [DataMember]
            public MultiPagePolicy MultiPageOption { get; set; }
            [DataMember]
            public ArchivePolicy ArchiveOption { get; set; }
            [DataMember]
            public string ArchiveSeparater { get; set; }
        }
    }

}
