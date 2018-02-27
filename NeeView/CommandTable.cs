﻿// Copyright (c) 2016-2018 Mitsuhiro Ito (nee)
//
// This software is released under the MIT License.
// http://opensource.org/licenses/mit-license.php

using NeeLaboratory.ComponentModel;
using NeeView.Windows.Property;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Resources;

// TODO: コマンド引数にコマンドパラメータを渡せないだろうか。（現状メニュー呼び出しであることを示すタグが指定されることが有る)

namespace NeeView
{
    public enum InputSceme
    {
        TypeA, // 標準
        TypeB, // ホイールでページ送り
        TypeC, // クリックでページ送り
    };

    public class CommandChangedEventArgs : EventArgs
    {
        /// <summary>
        /// キーバインド反映を保留
        /// </summary>
        public bool OnHold;

        public CommandChangedEventArgs(bool onHold)
        {
            this.OnHold = onHold;
        }
    }

    /// <summary>
    /// コマンド設定テーブル
    /// </summary>
    public class CommandTable : BindableBase, IEnumerable<KeyValuePair<CommandType, CommandElement>>
    {
        // SystemObject
        public static CommandTable Current { get; private set; }

        #region Fields

        private static Memento s_defaultMemento;

        private Dictionary<CommandType, CommandElement> _elements;
        private Models _models;
        private BookHub _book;
        private bool _isReversePageMove = true;
        private bool _isReversePageMoveWheel;

        #endregion

        #region Constructors

        // コンストラクタ
        public CommandTable()
        {
            if (Current != null) throw new InvalidOperationException();
            Current = this;

            InitializeCommandTable();
        }

        #endregion

        #region Events

        /// <summary>
        /// コマンドテーブルが変更された
        /// </summary>
        public event EventHandler<CommandChangedEventArgs> Changed;

        #endregion

        #region Properties

        // インテグザ
        public CommandElement this[CommandType key]
        {
            get
            {
                if (!_elements.ContainsKey(key)) throw new ArgumentOutOfRangeException(key.ToString());
                return _elements[key];
            }
            set { _elements[key] = value; }
        }


        [PropertyMember("スライダー方向によってページ移動コマンドの移動方向を入れ替える", Tips = "スライダーが左から右方向のときにページ移動方向を逆にします。")]
        public bool IsReversePageMove
        {
            get { return _isReversePageMove; }
            set { if (_isReversePageMove != value) { _isReversePageMove = value; RaisePropertyChanged(); } }
        }

        [PropertyMember("ホイール操作のときに入れ替える", Tips = "ホイール操作のみ対応の選択ができます。")]
        public bool IsReversePageMoveWheel
        {
            get { return _isReversePageMoveWheel; }
            set { if (_isReversePageMoveWheel != value) { _isReversePageMoveWheel = value; RaisePropertyChanged(); } }
        }

        #endregion

        #region IEnumerable Support

        // Enumerator
        public IEnumerator<KeyValuePair<CommandType, CommandElement>> GetEnumerator()
        {
            foreach (var pair in _elements)
            {
                yield return pair;
            }
        }

        // Enumerator
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        #endregion

        #region Methods

        //
        public bool TryGetValue(CommandType key, out CommandElement command)
        {
            return _elements.TryGetValue(key, out command);
        }

        /// <summary>
        /// 初期設定生成
        /// </summary>
        /// <param name="type">入力スキーム</param>
        /// <returns></returns>
        public static Memento CreateDefaultMemento(InputSceme type)
        {
            var memento = s_defaultMemento.Clone();

            // Type.M
            switch (type)
            {
                case InputSceme.TypeA: // default
                    break;

                case InputSceme.TypeB: // wheel page, right click contextmenu
                    memento.Elements[CommandType.NextScrollPage].ShortCutKey = null;
                    memento.Elements[CommandType.PrevScrollPage].ShortCutKey = null;
                    memento.Elements[CommandType.NextPage].ShortCutKey = "Left,WheelDown";
                    memento.Elements[CommandType.PrevPage].ShortCutKey = "Right,WheelUp";
                    memento.Elements[CommandType.OpenContextMenu].ShortCutKey = "RightClick";
                    break;

                case InputSceme.TypeC: // click page
                    memento.Elements[CommandType.NextScrollPage].ShortCutKey = null;
                    memento.Elements[CommandType.PrevScrollPage].ShortCutKey = null;
                    memento.Elements[CommandType.NextPage].ShortCutKey = "Left,LeftClick";
                    memento.Elements[CommandType.PrevPage].ShortCutKey = "Right,RightClick";
                    memento.Elements[CommandType.ViewScrollUp].ShortCutKey = "WheelUp";
                    memento.Elements[CommandType.ViewScrollDown].ShortCutKey = "WheelDown";
                    break;
            }

            return memento;
        }


        // コマンドターゲット設定
        public void SetTarget(Models models)
        {
            _models = models;
            ////_VM = vm;
            _book = _models.BookHub;
        }

        // .. あまりかわらん
        public T Parameter<T>(CommandType commandType) where T : class
        {
            return _elements[commandType].Parameter as T;
        }


        // ショートカット重複チェック
        public List<CommandType> GetOverlapShortCut(string shortcut)
        {
            var overlaps = _elements
                .Where(e => !string.IsNullOrEmpty(e.Value.ShortCutKey) && e.Value.ShortCutKey.Split(',').Contains(shortcut))
                .Select(e => e.Key)
                .ToList();

            return overlaps;
        }

        // マウスジェスチャー重複チェック
        public List<CommandType> GetOverlapMouseGesture(string gesture)
        {
            var overlaps = _elements
                .Where(e => !string.IsNullOrEmpty(e.Value.MouseGesture) && e.Value.MouseGesture.Split(',').Contains(gesture))
                .Select(e => e.Key)
                .ToList();

            return overlaps;
        }

        // コマンドリストをブラウザで開く
        public void OpenCommandListHelp()
        {
            // グループ分け
            var groups = new Dictionary<string, List<CommandElement>>();
            foreach (var command in _elements.Values)
            {
                if (command.Group == "dummy") continue;

                if (!groups.ContainsKey(command.Group))
                {
                    groups.Add(command.Group, new List<CommandElement>());
                }

                groups[command.Group].Add(command);
            }


            // 
            Directory.CreateDirectory(Temporary.TempSystemDirectory);
            string fileName = System.IO.Path.Combine(Temporary.TempSystemDirectory, "CommandList.html");

            //
            using (var writer = new System.IO.StreamWriter(fileName, false))
            {
                writer.WriteLine(HtmlHelpUtility.CraeteHeader("NeeView Command List"));
                writer.WriteLine("<body><h1>NeeView コマンド一覧</h1>");

                writer.WriteLine("<p>操作が割り当てられていないコマンドは「設定ウィンドウ」の「コマンド設定」で設定することで使用可能です</p>");

                // グループごとに出力
                foreach (var pair in groups)
                {
                    writer.WriteLine($"<h3>{pair.Key}</h3>");
                    writer.WriteLine("<table>");
                    writer.WriteLine($"<th>コマンド<th>ショートカット<th>ジェスチャー<th>タッチ<th>説明<tr>");
                    foreach (var command in pair.Value)
                    {
                        writer.WriteLine($"<td>{command.Text}<td>{command.ShortCutKey}<td>{new MouseGestureSequence(command.MouseGesture).ToDispString()}<td>{command.TouchGesture}<td>{command.Note}<tr>");
                    }
                    writer.WriteLine("</table>");
                }
                writer.WriteLine("</body>");

                writer.WriteLine(HtmlHelpUtility.CreateFooter());
            }

            System.Diagnostics.Process.Start(fileName);
        }

        #endregion

        #region Methods: Initialize

        /// <summary>
        /// コマンドテーブル初期化
        /// </summary>
        private void InitializeCommandTable()
        {
            // コマンドの設定定義
            _elements = new Dictionary<CommandType, CommandElement>();

            // None
            // 欠番
            {
                var element = new CommandElement();
                element.Group = "dummy";
                element.Text = "dummy";
                element.Execute = (s, e) => { return; };
                _elements[CommandType.None] = element;
            }


            // LoadAs
            {
                var element = new CommandElement();
                element.Group = "ファイル";
                element.Text = "ファイルを開く";
                element.MenuText = "開く(_O)...";
                element.Note = "圧縮ファイルか画像ファイルを選択して開きます";
                element.ShortCutKey = "Ctrl+O";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.MainWindowModel.LoadAs();
                _elements[CommandType.LoadAs] = element;
            }

            // ReLoad
            {
                var element = new CommandElement();
                element.Group = "ファイル";
                element.Text = "再読み込み";
                element.Note = "ブックを再読み込みします";
                element.MouseGesture = "UD";
                element.CanExecute = () => _book.CanReload();
                element.Execute = (s, e) => _book.ReLoad();
                element.IsShowMessage = false;
                _elements[CommandType.ReLoad] = element;
            }

            // Unload
            {
                var element = new CommandElement();
                element.Group = "ファイル";
                element.Text = "閉じる";
                element.MenuText = "閉じる(_C)";
                element.Note = "開いているブックを閉じます";
                element.CanExecute = () => _book.CanUnload();
                element.Execute = (s, e) => _book.RequestUnload(true);
                element.IsShowMessage = false;
                _elements[CommandType.Unload] = element;
            }

            // OpenApplication
            {
                var element = new CommandElement();
                element.Group = "ファイル";
                element.Text = "外部アプリで開く";
                element.Note = "表示されている画像を外部アプリで開きます。設定ウィンドウの<code>外部アプリ</code>でアプリを設定します";
                element.Execute = (s, e) => _models.BookOperation.OpenApplication();
                element.CanExecute = () => _models.BookOperation.CanOpenFilePlace();
                element.IsShowMessage = false;
                _elements[CommandType.OpenApplication] = element;
            }
            // OpenFilePlace
            {
                var element = new CommandElement();
                element.Group = "ファイル";
                element.Text = "エクスプローラーで開く";
                element.Note = "表示しているページのファイルをエクスプローラーで開きます";
                element.Execute = (s, e) => _models.BookOperation.OpenFilePlace();
                element.CanExecute = () => _models.BookOperation.CanOpenFilePlace();
                element.IsShowMessage = false;
                _elements[CommandType.OpenFilePlace] = element;
            }
            // Export
            {
                var element = new CommandElement();
                element.Group = "ファイル";
                element.Text = "名前をつけてファイルに保存";
                element.MenuText = "保存(_S)...";
                element.Note = "画像をファイルに保存します";
                element.ShortCutKey = "Ctrl+S";
                element.Execute = (s, e) => _models.BookOperation.Export();
                element.CanExecute = () => _models.BookOperation.CanExport();
                element.IsShowMessage = false;
                _elements[CommandType.Export] = element;
            }
            // Print
            {
                var element = new CommandElement();
                element.Group = "ファイル";
                element.Text = "印刷";
                element.MenuText = "印刷(_P)...";
                element.Note = "画像を印刷します。";
                element.ShortCutKey = "Ctrl+P";
                //element.Execute = (s, e) => _VM.Print();
                element.CanExecute = () => _models.ContentCanvas.CanPrint();
                element.IsShowMessage = false;
                _elements[CommandType.Print] = element;
            }
            // DeleteFile
            {
                var element = new CommandElement();
                element.Group = "ファイル";
                element.Text = "ファイルを削除";
                element.MenuText = "削除(_D)";
                element.Note = "ファイルを削除します。圧縮ファイルの場合は削除できません。 ";
                element.ShortCutKey = "Delete";
                element.Execute = (s, e) => _models.BookOperation.DeleteFile();
                element.CanExecute = () => _models.BookOperation.CanDeleteFile();
                element.IsShowMessage = false;
                _elements[CommandType.DeleteFile] = element;
            }
            // DeleteBook
            {
                var element = new CommandElement();
                element.Group = "ファイル";
                element.Text = "ブックを削除";
                element.MenuText = "ブック削除(_D)";
                element.Note = "現在閲覧中のフォルダーまたは圧縮ファイルを削除します。 ";
                element.Execute = (s, e) => _models.BookOperation.DeleteBook();
                element.CanExecute = () => _models.BookOperation.CanDeleteBook();
                element.IsShowMessage = false;
                _elements[CommandType.DeleteBook] = element;
            }
            // CopyFile
            {
                var element = new CommandElement();
                element.Group = "ファイル";
                element.Text = "ファイルをコピー";
                element.MenuText = "コピー(_C)";
                element.Note = "ファイルをクリップボードにコピーします。";
                element.ShortCutKey = "Ctrl+C";
                element.Execute = (s, e) => _models.BookOperation.CopyToClipboard();
                element.CanExecute = () => _models.BookOperation.CanOpenFilePlace();
                element.IsShowMessage = true;
                _elements[CommandType.CopyFile] = element;
            }
            // CopyImage
            {
                var element = new CommandElement();
                element.Group = "ファイル";
                element.Text = "画像をコピー";
                element.MenuText = "画像コピー";
                element.Note = "画像をクリップボードにコピーします。2ページ表示の場合はメインとなるページのみコピーします";
                element.ShortCutKey = "Ctrl+Shift+C";
                element.Execute = (s, e) => _models.ContentCanvas.CopyImageToClipboard();
                element.CanExecute = () => _models.ContentCanvas.CanCopyImageToClipboard();
                element.IsShowMessage = true;
                _elements[CommandType.CopyImage] = element;
            }
            // Paste
            {
                var element = new CommandElement();
                element.Group = "ファイル";
                element.Text = "貼り付け";
                element.MenuText = "貼り付け(_V)";
                element.Note = "クリップボードのファイルや画像を貼り付けます";
                element.ShortCutKey = "Ctrl+V";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.ContentDropManager.LoadFromClipboard();
                element.CanExecute = () => _models.ContentDropManager.CanLoadFromClipboard();
                _elements[CommandType.Paste] = element;
            }


            // ClearHistory
            {
                var element = new CommandElement();
                element.Group = "ファイル";
                element.Text = "履歴を消去";
                element.Note = "履歴を全て削除します";
                element.Execute = (s, e) => _models.MainWindowModel.ClearHistory();
                element.IsShowMessage = true;
                _elements[CommandType.ClearHistory] = element;
            }

            // ToggleStretchMode
            {
                var element = new CommandElement();
                element.Group = "表示サイズ";
                element.Text = "表示サイズを切り替える";
                element.Note = "画像の表示サイズを順番に切り替えます";
                element.ShortCutKey = "LeftButton+WheelDown";
                element.Execute = (s, e) => _models.ContentCanvas.StretchMode = _models.ContentCanvas.GetToggleStretchMode((ToggleStretchModeCommandParameter)element.Parameter);
                element.ExecuteMessage = e => _models.ContentCanvas.GetToggleStretchMode((ToggleStretchModeCommandParameter)element.Parameter).ToAliasName();
                element.DefaultParameter = new ToggleStretchModeCommandParameter() { IsLoop = true };
                element.IsShowMessage = true;
                _elements[CommandType.ToggleStretchMode] = element;
            }
            // ToggleStretchModeReverse
            {
                var element = new CommandElement();
                element.Group = "表示サイズ";
                element.Text = "表示サイズを切り替える(逆順)";
                element.Note = "画像の表示サイズを順番に切り替えます(逆順)";
                element.ShortCutKey = "LeftButton+WheelUp";
                element.Execute = (s, e) => _models.ContentCanvas.StretchMode = _models.ContentCanvas.GetToggleStretchModeReverse((ToggleStretchModeCommandParameter)element.Parameter);
                element.ExecuteMessage = e => _models.ContentCanvas.GetToggleStretchModeReverse((ToggleStretchModeCommandParameter)element.Parameter).ToAliasName();
                element.DefaultParameter = new ShareCommandParameter() { CommandType = CommandType.ToggleStretchMode };
                element.IsShowMessage = true;
                _elements[CommandType.ToggleStretchModeReverse] = element;
            }
            // SetStretchModeNone
            {
                var element = new CommandElement();
                element.Group = "表示サイズ";
                element.Text = "オリジナルサイズ";
                element.Note = "画像のサイズそのままで表示します";
                element.Execute = (s, e) => _models.ContentCanvas.StretchMode = PageStretchMode.None;
                element.CreateIsCheckedBinding = () => BindingGenerator.StretchMode(PageStretchMode.None);
                element.IsShowMessage = true;
                _elements[CommandType.SetStretchModeNone] = element;
            }
            // SetStretchModeInside
            {
                var element = new CommandElement();
                element.Group = "表示サイズ";
                element.Text = "大きい場合ウィンドウサイズに合わせる";
                element.Note = "ウィンドウに収まるように画像を縮小して表示します";
                element.Execute = (s, e) => _models.ContentCanvas.SetStretchMode(PageStretchMode.Inside, ((StretchModeCommandParameter)element.Parameter).IsToggle);
                element.ExecuteMessage = e => element.Text + (_models.ContentCanvas.TestStretchMode(PageStretchMode.Inside, ((StretchModeCommandParameter)element.Parameter).IsToggle) ? "" : " OFF");
                element.CreateIsCheckedBinding = () => BindingGenerator.StretchMode(PageStretchMode.Inside);
                element.DefaultParameter = new StretchModeCommandParameter();
                element.IsShowMessage = true;
                _elements[CommandType.SetStretchModeInside] = element;
            }
            // SetStretchModeOutside
            {
                var element = new CommandElement();
                element.Group = "表示サイズ";
                element.Text = "小さい場合ウィンドウサイズに広げる";
                element.Note = "ウィンドウに収まるように画像をできるだけ拡大して表示します";
                element.Execute = (s, e) => _models.ContentCanvas.SetStretchMode(PageStretchMode.Outside, ((StretchModeCommandParameter)element.Parameter).IsToggle);
                element.ExecuteMessage = e => element.Text + (_models.ContentCanvas.TestStretchMode(PageStretchMode.Outside, ((StretchModeCommandParameter)element.Parameter).IsToggle) ? "" : " OFF");
                element.CreateIsCheckedBinding = () => BindingGenerator.StretchMode(PageStretchMode.Outside);
                element.DefaultParameter = new ShareCommandParameter() { CommandType = CommandType.SetStretchModeInside };
                element.IsShowMessage = true;
                _elements[CommandType.SetStretchModeOutside] = element;
            }
            // SetStretchModeUniform
            {
                var element = new CommandElement();
                element.Group = "表示サイズ";
                element.Text = "ウィンドウサイズに合わせる";
                element.Note = "画像をウィンドウサイズに合わせるよう拡大縮小します";
                element.Execute = (s, e) => _models.ContentCanvas.SetStretchMode(PageStretchMode.Uniform, ((StretchModeCommandParameter)element.Parameter).IsToggle);
                element.ExecuteMessage = e => element.Text + (_models.ContentCanvas.TestStretchMode(PageStretchMode.Uniform, ((StretchModeCommandParameter)element.Parameter).IsToggle) ? "" : " OFF");
                element.CreateIsCheckedBinding = () => BindingGenerator.StretchMode(PageStretchMode.Uniform);
                element.DefaultParameter = new ShareCommandParameter() { CommandType = CommandType.SetStretchModeInside };
                element.IsShowMessage = true;
                _elements[CommandType.SetStretchModeUniform] = element;
            }
            // SetStretchModeUniformToFill
            {
                var element = new CommandElement();
                element.Group = "表示サイズ";
                element.Text = "ウィンドウいっぱいに広げる";
                element.Note = "縦横どちらかをウィンドウサイズに合わせるように拡大縮小します。画像はウィンドウサイズより大きくなります";
                element.Execute = (s, e) => _models.ContentCanvas.SetStretchMode(PageStretchMode.UniformToFill, ((StretchModeCommandParameter)element.Parameter).IsToggle);
                element.ExecuteMessage = e => element.Text + (_models.ContentCanvas.TestStretchMode(PageStretchMode.UniformToFill, ((StretchModeCommandParameter)element.Parameter).IsToggle) ? "" : " OFF");
                element.CreateIsCheckedBinding = () => BindingGenerator.StretchMode(PageStretchMode.UniformToFill);
                element.DefaultParameter = new ShareCommandParameter() { CommandType = CommandType.SetStretchModeInside };
                element.IsShowMessage = true;
                _elements[CommandType.SetStretchModeUniformToFill] = element;
            }
            // SetStretchModeUniformToSize
            {
                var element = new CommandElement();
                element.Group = "表示サイズ";
                element.Text = "面積をウィンドウに合わせる";
                element.Note = "ウィンドウの面積と等しくなるように画像を拡大縮小します";
                element.Execute = (s, e) => _models.ContentCanvas.SetStretchMode(PageStretchMode.UniformToSize, ((StretchModeCommandParameter)element.Parameter).IsToggle);
                element.ExecuteMessage = e => element.Text + (_models.ContentCanvas.TestStretchMode(PageStretchMode.UniformToSize, ((StretchModeCommandParameter)element.Parameter).IsToggle) ? "" : " OFF");
                element.CreateIsCheckedBinding = () => BindingGenerator.StretchMode(PageStretchMode.UniformToSize);
                element.DefaultParameter = new ShareCommandParameter() { CommandType = CommandType.SetStretchModeInside };
                element.IsShowMessage = true;
                _elements[CommandType.SetStretchModeUniformToSize] = element;
            }
            // SetStretchModeUniformToVertical
            {
                var element = new CommandElement();
                element.Group = "表示サイズ";
                element.Text = "高さをウィンドウに合わせる";
                element.Note = "ウィンドウの高さに画像の高さを合わせるように拡大縮小します";
                element.Execute = (s, e) => _models.ContentCanvas.SetStretchMode(PageStretchMode.UniformToVertical, ((StretchModeCommandParameter)element.Parameter).IsToggle);
                element.ExecuteMessage = e => element.Text + (_models.ContentCanvas.TestStretchMode(PageStretchMode.UniformToVertical, ((StretchModeCommandParameter)element.Parameter).IsToggle) ? "" : " OFF");
                element.CreateIsCheckedBinding = () => BindingGenerator.StretchMode(PageStretchMode.UniformToVertical);
                element.DefaultParameter = new ShareCommandParameter() { CommandType = CommandType.SetStretchModeInside };
                element.IsShowMessage = true;
                _elements[CommandType.SetStretchModeUniformToVertical] = element;
            }

            // ToggleIsEnabledNearestNeighbor
            {
                var element = new CommandElement();
                element.Group = "エフェクト";
                element.Text = "ドットのまま拡大ON/OFF";
                element.MenuText = "ドットのまま拡大";
                element.Note = "ONにすると拡大するときにドットのまま拡大します。OFFの時にはスケール変換処理(Fant)が行われます";
                element.Execute = (s, e) => _models.ContentCanvas.IsEnabledNearestNeighbor = !_models.ContentCanvas.IsEnabledNearestNeighbor;
                element.ExecuteMessage = e => _models.ContentCanvas.IsEnabledNearestNeighbor ? "高品質に拡大する" : "ドットのまま拡大する";
                element.CreateIsCheckedBinding = () => new Binding(nameof(_models.ContentCanvas.IsEnabledNearestNeighbor)) { Source = _models.ContentCanvas };
                element.IsShowMessage = true;
                _elements[CommandType.ToggleIsEnabledNearestNeighbor] = element;
            }

            // ToggleBackground
            {
                var element = new CommandElement();
                element.Group = "背景";
                element.Text = "背景を切り替える";
                element.Note = "背景を順番に切り替えます";
                element.Execute = (s, e) => _models.ContentCanvasBrush.Background = _models.ContentCanvasBrush.Background.GetToggle();
                element.ExecuteMessage = e => _models.ContentCanvasBrush.Background.GetToggle().ToAliasName();
                element.IsShowMessage = true;
                _elements[CommandType.ToggleBackground] = element;
            }

            // SetBackgroundBlack
            {
                var element = new CommandElement();
                element.Group = "背景";
                element.Text = "背景を黒色にする";
                element.Note = "背景を黒色にします";
                element.Execute = (s, e) => _models.ContentCanvasBrush.Background = BackgroundStyle.Black;
                element.CreateIsCheckedBinding = () => BindingGenerator.Background(BackgroundStyle.Black);
                element.IsShowMessage = true;
                _elements[CommandType.SetBackgroundBlack] = element;
            }

            // SetBackgroundWhite
            {
                var element = new CommandElement();
                element.Group = "背景";
                element.Text = "背景を白色にする";
                element.Note = "背景を白色にします";
                element.Execute = (s, e) => _models.ContentCanvasBrush.Background = BackgroundStyle.White;
                element.CreateIsCheckedBinding = () => BindingGenerator.Background(BackgroundStyle.White);
                element.IsShowMessage = true;
                _elements[CommandType.SetBackgroundWhite] = element;
            }

            // SetBackgroundAuto
            {
                var element = new CommandElement();
                element.Group = "背景";
                element.Text = "背景を画像に合わせた色にする";
                element.Note = "背景色を画像から設定します。具体的には画像の左上ピクセルの色が使用されます";
                element.Execute = (s, e) => _models.ContentCanvasBrush.Background = BackgroundStyle.Auto;
                element.CreateIsCheckedBinding = () => BindingGenerator.Background(BackgroundStyle.Auto);
                element.IsShowMessage = true;
                _elements[CommandType.SetBackgroundAuto] = element;
            }

            // SetBackgroundCheck
            {
                var element = new CommandElement();
                element.Group = "背景";
                element.Text = "背景をチェック模様にする";
                element.Note = "背景をチェック模様にします";
                element.Execute = (s, e) => _models.ContentCanvasBrush.Background = BackgroundStyle.Check;
                element.CreateIsCheckedBinding = () => BindingGenerator.Background(BackgroundStyle.Check);
                element.IsShowMessage = true;
                _elements[CommandType.SetBackgroundCheck] = element;
            }

            // SetBackgroundCustom
            {
                var element = new CommandElement();
                element.Group = "背景";
                element.Text = "背景をカスタム背景にする";
                element.Note = "背景をカスタム背景にします";
                element.Execute = (s, e) => _models.ContentCanvasBrush.Background = BackgroundStyle.Custom;
                element.CreateIsCheckedBinding = () => BindingGenerator.Background(BackgroundStyle.Custom);
                element.IsShowMessage = true;
                _elements[CommandType.SetBackgroundCustom] = element;
            }

            // ToggleTopmost
            {
                var element = new CommandElement();
                element.Group = "ウィンドウ";
                element.Text = "常に手前に表示ON/OFF";
                element.MenuText = "常に手前に表示";
                element.Note = "ウィンドウを常に手前に表示します";
                element.Execute = (s, e) => WindowShape.Current.ToggleTopmost();
                element.ExecuteMessage = e => WindowShape.Current.IsTopmost ? "「常に手前に表示」を解除" : "常に手前に表示する";
                element.CanExecute = () => true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(WindowShape.IsTopmost)) { Source = WindowShape.Current, Mode = BindingMode.OneWay };
                element.IsShowMessage = true;
                _elements[CommandType.ToggleTopmost] = element;
            }
            // ToggleHideMenu
            {
                var element = new CommandElement();
                element.Group = "ウィンドウ";
                element.Text = "メニューを自動的に隠すON/OFF";
                element.MenuText = "メニューを自動的に隠す";
                element.Note = "メニューを非表示にします。カーソルをウィンドウ上端に合わせることで表示されます";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.MainWindowModel.ToggleHideMenu();
                element.ExecuteMessage = e => _models.MainWindowModel.IsHideMenu ? "メニューを表示する" : "メニューを自動的に隠す";
                element.CanExecute = () => true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(_models.MainWindowModel.IsHideMenu)) { Source = _models.MainWindowModel };
                _elements[CommandType.ToggleHideMenu] = element;
            }
            // ToggleHidePageSlider
            {
                var element = new CommandElement();
                element.Group = "ウィンドウ";
                element.Text = "スライダーを自動的に隠すON/OFF";
                element.MenuText = "スライダーを自動的に隠す";
                element.Note = "スライダーを非表示にします。カーソルをウィンドウ下端に合わせることで表示されます";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.MainWindowModel.ToggleHidePageSlider();
                element.ExecuteMessage = e => _models.MainWindowModel.IsHidePageSlider ? "スライダーを表示する" : "スライダーを自動的に隠す";
                element.CanExecute = () => true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(_models.MainWindowModel.IsHidePageSlider)) { Source = _models.MainWindowModel };
                _elements[CommandType.ToggleHidePageSlider] = element;
            }
            // ToggleHidePanel
            {
                var element = new CommandElement();
                element.Group = "ウィンドウ";
                element.Text = "パネルを自動的に隠すON/OFF";
                element.MenuText = "パネルを自動的に隠す";
                element.Note = "左右のパネルを自動的に隠します。カーソルをウィンドウ左端、右端に合わせることで表示されます";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.MainWindowModel.ToggleHidePanel();
                element.ExecuteMessage = e => _models.MainWindowModel.IsHidePanel ? "パネルを表示する" : "パネルを自動的に隠す";
                element.CanExecute = () => true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(_models.MainWindowModel.IsHidePanel)) { Source = _models.MainWindowModel };
                _elements[CommandType.ToggleHidePanel] = element;
            }


            // ToggleVisibleTitleBar
            {
                var element = new CommandElement();
                element.Group = "ウィンドウ";
                element.Text = "タイトルバーON/OFF";
                element.MenuText = "タイトルバー";
                element.Note = "ウィンドウタイトルバーの表示/非表示を切り替えます";
                element.IsShowMessage = false;
                element.Execute = (s, e) => WindowShape.Current.ToggleCaptionVisible();
                element.ExecuteMessage = e => WindowShape.Current.IsCaptionVisible ? "タイトルバーを消す" : "タイトルバー表示する";
                element.CanExecute = () => true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(WindowShape.IsCaptionVisible)) { Source = WindowShape.Current, Mode = BindingMode.OneWay };
                _elements[CommandType.ToggleVisibleTitleBar] = element;
            }
            // ToggleVisibleAddressBar
            {
                var element = new CommandElement();
                element.Group = "ウィンドウ";
                element.Text = "アドレスバーON/OFF";
                element.MenuText = "アドレスバー";
                element.Note = "アドレスバーの表示/非表示を切り替えます";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.MainWindowModel.ToggleVisibleAddressBar();
                element.ExecuteMessage = e => _models.MainWindowModel.IsVisibleAddressBar ? "アドレスバーを消す" : "アドレスバーを表示する";
                element.CanExecute = () => true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(_models.MainWindowModel.IsVisibleAddressBar)) { Source = _models.MainWindowModel };
                _elements[CommandType.ToggleVisibleAddressBar] = element;
            }
            // ToggleVisibleSideBar
            {
                var element = new CommandElement();
                element.Group = "ウィンドウ";
                element.Text = "サイドバーON/OFF";
                element.MenuText = "サイドバー";
                element.Note = "サイドバーの表示/非表示を切り替えます";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.SidePanel.IsSideBarVisible = !_models.SidePanel.IsSideBarVisible;
                element.ExecuteMessage = e => _models.SidePanel.IsSideBarVisible ? "サイドバーを消す" : "サイドバーを表示する";
                element.CanExecute = () => true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(SidePanel.IsSideBarVisible)) { Source = _models.SidePanel };
                _elements[CommandType.ToggleVisibleSideBar] = element;
            }
            // ToggleVisibleFileInfo
            {
                var element = new CommandElement();
                element.Group = "パネル";
                element.Text = "ファイル情報の表示ON/OFF";
                element.MenuText = "ファイル情報";
                element.Note = "ファイル情報パネルの表示/非表示を切り替えます。";
                element.ShortCutKey = "I";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.SidePanel.ToggleVisibleFileInfo(e is MenuCommandTag);
                element.ExecuteMessage = e => _models.SidePanel.IsVisibleFileInfo ? "ファイル情報を消す" : "ファイル情報を表示する";
                element.CanExecute = () => true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(SidePanel.IsVisibleFileInfo)) { Source = _models.SidePanel };
                _elements[CommandType.ToggleVisibleFileInfo] = element;
            }
            // ToggleVisibleEffectInfo
            {
                var element = new CommandElement();
                element.Group = "パネル";
                element.Text = "エフェクトパネルの表示ON/OFF";
                element.MenuText = "エフェクトパネル";
                element.Note = "エフェクトパネルの表示/非表示を切り替えます。";
                element.ShortCutKey = "E";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.SidePanel.ToggleVisibleEffectInfo(e is MenuCommandTag);
                element.ExecuteMessage = e => _models.SidePanel.IsVisibleEffectInfo ? "エフェクトパネルを消す" : "エフェクトパネルを表示する";
                element.CanExecute = () => true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(SidePanel.IsVisibleEffectInfo)) { Source = _models.SidePanel };
                _elements[CommandType.ToggleVisibleEffectInfo] = element;
            }
            // ToggleVisibleFolderList
            {
                var element = new CommandElement();
                element.Group = "パネル";
                element.Text = "フォルダーリストの表示ON/OFF";
                element.MenuText = "フォルダーリスト";
                element.Note = "フォルダーリストパネルの表示/非表示を切り替えます";
                element.ShortCutKey = "F";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.SidePanel.ToggleVisibleFolderList(e is MenuCommandTag);
                element.ExecuteMessage = e => _models.SidePanel.IsVisibleFolderList ? "フォルダーリストを消す" : "フォルダーリストを表示する";
                element.CanExecute = () => true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(SidePanel.IsVisibleFolderList)) { Source = _models.SidePanel };
                _elements[CommandType.ToggleVisibleFolderList] = element;
            }
            // ToggleVisibleBookmarkList
            {
                var element = new CommandElement();
                element.Group = "パネル";
                element.Text = "ブックマークの表示ON/OFF";
                element.MenuText = "ブックマークリスト";
                element.Note = "ブックマークリストパネルの表示/非表示を切り替えます";
                element.ShortCutKey = "B";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.SidePanel.ToggleVisibleBookmarkList(e is MenuCommandTag);
                element.ExecuteMessage = e => _models.SidePanel.IsVisibleBookmarkList ? "ブックマークリストを消す" : "ブックマークリストを表示する";
                element.CanExecute = () => true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(SidePanel.IsVisibleBookmarkList)) { Source = _models.SidePanel };
                _elements[CommandType.ToggleVisibleBookmarkList] = element;
            }
            // ToggleVisiblePagemarkList
            {
                var element = new CommandElement();
                element.Group = "パネル";
                element.Text = "ページマークの表示ON/OFF";
                element.MenuText = "ページマークリスト";
                element.Note = "ページマークリストパネルの表示/非表示を切り替えます";
                element.ShortCutKey = "M";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.SidePanel.ToggleVisiblePagemarkList(e is MenuCommandTag);
                element.ExecuteMessage = e => _models.SidePanel.IsVisiblePagemarkList ? "ページマークリストを消す" : "ページマークリストを表示する";
                element.CanExecute = () => true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(SidePanel.IsVisiblePagemarkList)) { Source = _models.SidePanel };
                _elements[CommandType.ToggleVisiblePagemarkList] = element;
            }
            // ToggleVisibleHistoryList
            {
                var element = new CommandElement();
                element.Group = "パネル";
                element.Text = "履歴リストの表示ON/OFF";
                element.MenuText = "履歴リスト";
                element.Note = "履歴リストパネルの表示/非表示を切り替えます";
                element.ShortCutKey = "H";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.SidePanel.ToggleVisibleHistoryList(e is MenuCommandTag);
                element.ExecuteMessage = e => _models.SidePanel.IsVisibleHistoryList ? "履歴リストを消す" : "履歴リストを表示する";
                element.CanExecute = () => true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(SidePanel.IsVisibleHistoryList)) { Source = _models.SidePanel };
                _elements[CommandType.ToggleVisibleHistoryList] = element;
            }
            // ToggleVisiblePageList
            {
                var element = new CommandElement();
                element.Group = "パネル";
                element.Text = "ページリストの表示ON/OFF";
                element.MenuText = "ページリスト";
                element.Note = "ページリスト表示/非表示を切り替えます。フォルダーリストは表示状態になります";
                element.ShortCutKey = "P";
                element.IsShowMessage = false;
                element.ExecuteMessage = e => _models.SidePanel.IsVisiblePageListMenu ? "ページリストを消す" : "ページリストを表示する";
                element.Execute = (s, e) => _models.SidePanel.ToggleVisiblePageList(e is MenuCommandTag);
                element.CanExecute = () => true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(_models.FolderPanelModel.IsPageListVisible)) { Source = _models.FolderPanelModel, Mode = BindingMode.OneWay };
                _elements[CommandType.ToggleVisiblePageList] = element;
            }
            // ToggleVisibleFolderSearchBox
            {
                var element = new CommandElement();
                element.Group = "パネル";
                element.Text = "検索ボックスの表示ON/OFF";
                element.MenuText = "検索ボックス";
                element.Note = "検索ボックス表示/非表示を切り替えます。フォルダーリストは表示状態になります";
                element.IsShowMessage = false;
                element.ExecuteMessage = e => _models.SidePanel.IsVisibleFolderSearchBox ? "検索ボックスを消す" : "検索ボックスを表示する";
                element.Execute = (s, e) => _models.SidePanel.ToggleVisibleFolderSearchBox(e is MenuCommandTag);
                element.CanExecute = () => true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(_models.FolderList.IsFolderSearchBoxVisible)) { Source = _models.FolderList, Mode = BindingMode.OneWay };
                _elements[CommandType.ToggleVisibleFolderSearchBox] = element;
            }

            // ToggleVisibleThumbnailList
            {
                var element = new CommandElement();
                element.Group = "サムネイルリスト";
                element.Text = "サムネイルリストの表示ON/OFF";
                element.MenuText = "サムネイルリスト";
                element.Note = "サムネイルリスト表示/非表示を切り替えます";
                element.IsShowMessage = false;
                element.ExecuteMessage = e => _models.ThumbnailList.IsEnableThumbnailList ? "サムネイルリストを消す" : "サムネイルリストを表示する";
                element.Execute = (s, e) => _models.ThumbnailList.ToggleVisibleThumbnailList();
                element.CanExecute = () => true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(_models.ThumbnailList.IsEnableThumbnailList)) { Source = _models.ThumbnailList };
                _elements[CommandType.ToggleVisibleThumbnailList] = element;
            }
            // ToggleHideThumbnailList
            {
                var element = new CommandElement();
                element.Group = "サムネイルリスト";
                element.Text = "サムネイルリストを自動的に隠すON/OFF";
                element.MenuText = "サムネイルリストを自動的に隠す";
                element.Note = "スライダーを使用している時だけサムネイルリストを表示するようにします";
                element.IsShowMessage = false;
                element.ExecuteMessage = e => _models.ThumbnailList.IsHideThumbnailList ? "サムネイルリストを表示する" : "サムネイルリストを自動的に隠す";
                element.Execute = (s, e) => _models.ThumbnailList.ToggleHideThumbnailList();
                element.CanExecute = () => true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(_models.ThumbnailList.IsHideThumbnailList)) { Source = _models.ThumbnailList };
                _elements[CommandType.ToggleHideThumbnailList] = element;
            }


            // ToggleFullScreen
            {
                var element = new CommandElement();
                element.Group = "ウィンドウ";
                element.Text = "フルスクリーンON/OFF";
                element.MenuText = "フルスクリーン";
                element.Note = "フルスクリーン状態を切替ます";
                element.ShortCutKey = "F11";
                element.MouseGesture = "U";
                element.IsShowMessage = false;
                element.Execute = (s, e) => WindowShape.Current.ToggleFullScreen();
                element.ExecuteMessage = e => WindowShape.Current.IsFullScreen ? "フルスクリーンOFF" : "フルスクリーンON";
                element.CanExecute = () => true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(WindowShape.Current.IsFullScreen)) { Source = WindowShape.Current, Mode = BindingMode.OneWay };
                _elements[CommandType.ToggleFullScreen] = element;
            }
            // SetFullScreen
            {
                var element = new CommandElement();
                element.Group = "ウィンドウ";
                element.Text = "フルスクリーンにする";
                element.Note = "フルスクリーンにします";
                element.IsShowMessage = false;
                element.Execute = (s, e) => WindowShape.Current.SetFullScreen(true);
                element.CanExecute = () => true;
                _elements[CommandType.SetFullScreen] = element;
            }
            // CancelFullScreen
            {
                var element = new CommandElement();
                element.Group = "ウィンドウ";
                element.Text = "フルスクリーン解除";
                element.Note = "フルスクリーンを解除します";
                element.IsShowMessage = false;
                element.Execute = (s, e) => WindowShape.Current.SetFullScreen(false);
                element.CanExecute = () => true;
                _elements[CommandType.CancelFullScreen] = element;
            }

            // ToggleWindowMinimize
            {
                var element = new CommandElement();
                element.Group = "ウィンドウ";
                element.Text = "ウィンドウを最小化する";
                element.MenuText = "ウィンドウ最小化";
                element.Note = "ウィンドウを最小化します";
                element.IsShowMessage = false;
                element.CanExecute = () => true;
                _elements[CommandType.ToggleWindowMinimize] = element;
            }

            // ToggleWindowMaximize
            {
                var element = new CommandElement();
                element.Group = "ウィンドウ";
                element.Text = "ウィンドウを最大化する";
                element.MenuText = "ウィンドウ最大化";
                element.Note = "ウィンドウを最大化します。既に最大化されている場合は元のサイズに戻します。";
                element.IsShowMessage = false;
                element.CanExecute = () => true;
                _elements[CommandType.ToggleWindowMaximize] = element;
            }

            // ShowHiddenPanels
            {
                var element = new CommandElement();
                element.Group = "ウィンドウ";
                element.Text = "パネルを一時的に表示する";
                element.MenuText = "パネル一時表示";
                element.Note = "自動非表示になっているパネルを一時的に表示します。なんらかの操作をすると解除されます";
                element.TouchGesture = "TouchCenter";
                element.CanExecute = () => true;
                element.Execute = (s, e) => _models.MainWindowModel.EnterVisibleLocked();
                element.IsShowMessage = false;
                _elements[CommandType.ShowHiddenPanels] = element;
            }


            // ToggleSlideShow
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "スライドショー再生/停止";
                element.MenuText = "スライドショー";
                element.Note = "スライドショーの再生/停止を切り替えます";
                element.ShortCutKey = "F5";
                element.Execute = (s, e) => _models.SlideShow.ToggleSlideShow();
                element.ExecuteMessage = e => _models.SlideShow.IsPlayingSlideShow ? "スライドショー停止" : "スライドショー再生";
                element.CreateIsCheckedBinding = () => new Binding(nameof(SlideShow.IsPlayingSlideShow)) { Source = _models.SlideShow };
                element.IsShowMessage = true;
                _elements[CommandType.ToggleSlideShow] = element;
            }
            // ViewScrollUp
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "スクロール↑";
                element.Note = "画像を上方向にするロールさせます。縦スクロールできないときは横スクロールになります";
                element.IsShowMessage = false;
                element.DefaultParameter = new ViewScrollCommandParameter() { Scroll = 25, AllowCrossScroll = true };
                element.Execute = (s, e) => _models.DragTransformControl.ScrollUp((ViewScrollCommandParameter)element.Parameter);
                _elements[CommandType.ViewScrollUp] = element;
            }
            // ViewScrollDown
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "スクロール↓";
                element.Note = "画像を下方向にするロールさせます。縦スクロールできないときは横スクロールになります";
                element.IsShowMessage = false;
                element.DefaultParameter = new ShareCommandParameter() { CommandType = CommandType.ViewScrollUp };
                element.Execute = (s, e) => _models.DragTransformControl.ScrollDown((ViewScrollCommandParameter)element.Parameter);
                _elements[CommandType.ViewScrollDown] = element;
            }
            // ViewScrollLeft
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "スクロール←";
                element.Note = "画像を左方向にするロールさせます。横スクロールできないときは縦スクロールになります";
                element.IsShowMessage = false;
                element.DefaultParameter = new ShareCommandParameter() { CommandType = CommandType.ViewScrollUp };
                element.Execute = (s, e) => _models.DragTransformControl.ScrollLeft((ViewScrollCommandParameter)element.Parameter);
                _elements[CommandType.ViewScrollLeft] = element;
            }
            // ViewScrollRight
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "スクロール→";
                element.Note = "画像を右方向にするロールさせます。横スクロールできないときは縦スクロールになります";
                element.IsShowMessage = false;
                element.DefaultParameter = new ShareCommandParameter() { CommandType = CommandType.ViewScrollUp };
                element.Execute = (s, e) => _models.DragTransformControl.ScrollRight((ViewScrollCommandParameter)element.Parameter);
                _elements[CommandType.ViewScrollRight] = element;
            }
            // ViewScaleUp
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "拡大";
                element.Note = "画像を拡大します";
                element.ShortCutKey = "RightButton+WheelUp";
                element.IsShowMessage = false;
                element.DefaultParameter = new ViewScaleCommandParameter() { Scale = 20, IsSnapDefaultScale = true };
                element.Execute = (s, e) => { var param = (ViewScaleCommandParameter)element.Parameter; _models.DragTransformControl.ScaleUp(param.Scale / 100.0, param.IsSnapDefaultScale); };
                _elements[CommandType.ViewScaleUp] = element;
            }
            // ViewScaleDown
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "縮小";
                element.Note = "画像を縮小します";
                element.ShortCutKey = "RightButton+WheelDown";
                element.IsShowMessage = false;
                element.DefaultParameter = new ShareCommandParameter() { CommandType = CommandType.ViewScaleUp };
                ////element.Execute = (s, e) => _models.DragTransformControl.ScaleDown(((ViewScaleCommandParameter)element.Parameter).Scale / 100.0);
                element.Execute = (s, e) => { var param = (ViewScaleCommandParameter)element.Parameter; _models.DragTransformControl.ScaleDown(param.Scale / 100.0, param.IsSnapDefaultScale); };
                _elements[CommandType.ViewScaleDown] = element;
            }
            // ViewRotateLeft
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "左回転";
                element.Note = "画像を左回転させます";
                element.IsShowMessage = false;
                element.DefaultParameter = new ViewRotateCommandParameter() { Angle = 45 };
                element.Execute = (s, e) => _models.ContentCanvas.ViewRotateLeft((ViewRotateCommandParameter)element.Parameter);
                _elements[CommandType.ViewRotateLeft] = element;
            }
            // ViewRotateRight
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "右回転";
                element.Note = "画像を右回転させます";
                element.IsShowMessage = false;
                element.DefaultParameter = new ShareCommandParameter() { CommandType = CommandType.ViewRotateLeft };
                element.Execute = (s, e) => _models.ContentCanvas.ViewRotateRight((ViewRotateCommandParameter)element.Parameter);
                _elements[CommandType.ViewRotateRight] = element;
            }


            // ToggleIsAutoRotate
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "自動回転ON/OFF";
                element.MenuText = "自動回転";
                element.Note = "ページ表示時、縦長画像を90度回転します。ウィンドウが縦長の場合、横長画像を90度回転します";
                element.Execute = (s, e) => _models.ContentCanvas.ToggleAutoRotate();
                element.ExecuteMessage = e => _models.ContentCanvas.IsAutoRotate ? "自動回転OFF" : "自動回転ON";
                element.CreateIsCheckedBinding = () => new Binding(nameof(ContentCanvas.IsAutoRotate)) { Source = _models.ContentCanvas };
                element.DefaultParameter = new AutoRotateCommandParameter();
                element.IsShowMessage = true;
                _elements[CommandType.ToggleIsAutoRotate] = element;
            }

            // ToggleViewFlipHorizontal
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "左右反転";
                element.Note = "画像を左右反転させます";
                element.IsShowMessage = false;
                element.CreateIsCheckedBinding = () => BindingGenerator.IsFlipHorizontal();
                element.Execute = (s, e) => _models.DragTransformControl.ToggleFlipHorizontal();
                _elements[CommandType.ToggleViewFlipHorizontal] = element;
            }
            // ViewFlipHorizontalOn
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "左右反転ON";
                element.Note = "左右反転状態にします";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.DragTransformControl.FlipHorizontal(true);
                _elements[CommandType.ViewFlipHorizontalOn] = element;
            }
            // ViewFlipHorizontalOff
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "左右反転OFF";
                element.Note = "左右反転状態を解除します";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.DragTransformControl.FlipHorizontal(false);
                _elements[CommandType.ViewFlipHorizontalOff] = element;
            }


            // ToggleViewFlipVertical
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "上下反転";
                element.Note = "画像を上下反転させます";
                element.IsShowMessage = false;
                element.CreateIsCheckedBinding = () => BindingGenerator.IsFlipVertical();
                element.Execute = (s, e) => _models.DragTransformControl.ToggleFlipVertical();
                _elements[CommandType.ToggleViewFlipVertical] = element;
            }
            // ViewFlipVerticalOn
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "上下反転ON";
                element.Note = "上下反転状態にします";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.DragTransformControl.FlipVertical(true);
                _elements[CommandType.ViewFlipVerticalOn] = element;
            }
            // ViewFlipVerticalOff
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "上下反転OFF";
                element.Note = "上下反転状態を解除します";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.DragTransformControl.FlipVertical(false);
                _elements[CommandType.ViewFlipVerticalOff] = element;
            }

            // ViewReset
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "ビューリセット";
                element.Note = "ビュー操作での回転、拡縮、移動、反転を初期化します";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.ContentCanvas.ResetTransform(true);
                _elements[CommandType.ViewReset] = element;
            }

            // PrevPage
            {
                var element = new CommandElement();
                element.Group = "移動";
                element.Text = "前のページに戻る";
                element.Note = "ページ前方向に移動します。2ページ表示の場合は2ページ分移動します";
                element.ShortCutKey = "Right,RightClick";
                element.TouchGesture = "TouchR1,TouchR2";
                element.MouseGesture = "R";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.BookOperation.PrevPage();
                element.PairPartner = CommandType.NextPage;
                _elements[CommandType.PrevPage] = element;
            }
            // NextPage
            {
                var element = new CommandElement();
                element.Group = "移動";
                element.Text = "次のページへ進む";
                element.Note = "ページ次方向に移動します。2ページ表示の場合は2ページ分移動します";
                element.ShortCutKey = "Left,LeftClick";
                element.TouchGesture = "TouchL1,TouchL2";
                element.MouseGesture = "L";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.BookOperation.NextPage();
                element.PairPartner = CommandType.PrevPage;
                _elements[CommandType.NextPage] = element;
            }
            // PrevOnePage
            {
                var element = new CommandElement();
                element.Group = "移動";
                element.Text = "1ページ戻る";
                element.Note = "1ページだけ前方向に移動します";
                element.MouseGesture = "LR";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.BookOperation.PrevOnePage();
                element.PairPartner = CommandType.NextOnePage;
                _elements[CommandType.PrevOnePage] = element;
            }
            // NextOnePage
            {
                var element = new CommandElement();
                element.Group = "移動";
                element.Text = "1ページ進む";
                element.Note = "1ページだけ次方向に移動します";
                element.MouseGesture = "RL";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.BookOperation.NextOnePage();
                element.PairPartner = CommandType.PrevOnePage;
                _elements[CommandType.NextOnePage] = element;
            }


            // PrevScrollPage
            {
                var element = new CommandElement();
                element.Group = "移動";
                element.Text = "スクロール＋前のページに戻る";
                element.Note = "ページ前方向に画像をスクロールさせます。スクロールできない場合は前ページに移動します。ルーペ使用時はページ移動のみ行います。";
                element.ShortCutKey = "WheelUp";
                element.IsShowMessage = false;
                element.DefaultParameter = new ScrollPageCommandParameter() { IsNScroll = true, IsAnimation = true, Margin = 50, Scroll = 100 };
                element.Execute = (s, e) => _models.MainWindowModel.PrevScrollPage();
                element.PairPartner = CommandType.NextScrollPage;
                _elements[CommandType.PrevScrollPage] = element;
            }
            // NextScrollPage
            {
                var element = new CommandElement();
                element.Group = "移動";
                element.Text = "スクロール＋次のページへ進む";
                element.Note = "ページ次方向に画像をスクロールさせます。スクロールできない場合は次ページに移動します。ルーペ使用時はページ移動のみ行います。";
                element.ShortCutKey = "WheelDown";
                element.IsShowMessage = false;
                element.DefaultParameter = new ShareCommandParameter() { CommandType = CommandType.PrevScrollPage };
                element.Execute = (s, e) => _models.MainWindowModel.NextScrollPage();
                element.PairPartner = CommandType.PrevScrollPage;
                _elements[CommandType.NextScrollPage] = element;
            }
            // MovePageWithCursor
            {
                var element = new CommandElement();
                element.Group = "移動";
                element.Text = "マウス位置依存でページを前後させる";
                element.Note = "マウスカーソル位置によって移動方向が決まります。 ウィンドウ左にカーソルがあるときは次のページへ進み、右にカーソルがあるときは前のページに戻ります";
                element.IsShowMessage = false;
                _elements[CommandType.MovePageWithCursor] = element;
            }

            // PrevSizePage
            {
                var element = new CommandElement();
                element.Group = "移動";
                element.Text = "設定ページ数戻る";
                element.Note = "設定されたページ数だけ前方向に移動します";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.BookOperation.PrevSizePage(((MoveSizePageCommandParameter)element.Parameter).Size);
                element.DefaultParameter = new MoveSizePageCommandParameter() { Size = 10 };
                element.PairPartner = CommandType.NextSizePage;
                _elements[CommandType.PrevSizePage] = element;
            }

            // NextSizePage
            {
                var element = new CommandElement();
                element.Group = "移動";
                element.Text = "設定ページ数進む";
                element.Note = "設定されたページ数だけ次方向に移動します";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.BookOperation.NextSizePage(((MoveSizePageCommandParameter)element.Parameter).Size);
                element.DefaultParameter = new ShareCommandParameter() { CommandType = CommandType.PrevSizePage };
                element.PairPartner = CommandType.PrevSizePage;
                _elements[CommandType.NextSizePage] = element;
            }

            // FirstPage
            {
                var element = new CommandElement();
                element.Group = "移動";
                element.Text = "最初のページに移動";
                element.Note = "先頭ページに移動します";
                element.ShortCutKey = "Ctrl+Right";
                element.MouseGesture = "UR";
                element.Execute = (s, e) => _models.BookOperation.FirstPage();
                element.IsShowMessage = true;
                element.PairPartner = CommandType.LastPage;
                _elements[CommandType.FirstPage] = element;
            }
            // LastPage
            {
                var element = new CommandElement();
                element.Group = "移動";
                element.Text = "最後のページへ移動";
                element.Note = "終端ページに移動します";
                element.ShortCutKey = "Ctrl+Left";
                element.MouseGesture = "UL";
                element.Execute = (s, e) => _models.BookOperation.LastPage();
                element.IsShowMessage = true;
                element.PairPartner = CommandType.FirstPage;
                _elements[CommandType.LastPage] = element;
            }
            // PrevFolder
            {
                var element = new CommandElement();
                element.Group = "移動";
                element.Text = "前のブックに移動";
                element.Note = "フォルダーリスト上での前のブックを読み込みます";
                element.ShortCutKey = "Up";
                element.MouseGesture = "LU";
                element.IsShowMessage = false;
                element.Execute = async (s, e) => await _models.FolderList.PrevFolder();
                _elements[CommandType.PrevFolder] = element;
            }
            // NextFolder
            {
                var element = new CommandElement();
                element.Group = "移動";
                element.Text = "次のブックへ移動";
                element.Note = "フォルダーリスト上での次のブックを読み込みます";
                element.ShortCutKey = "Down";
                element.MouseGesture = "LD";
                element.IsShowMessage = false;
                element.Execute = async (s, e) => await _models.FolderList.NextFolder();
                _elements[CommandType.NextFolder] = element;
            }
            // PrevHistory
            {
                var element = new CommandElement();
                element.Group = "移動";
                element.Text = "前の履歴に戻る";
                element.Note = "前の古い履歴のブックを読み込みます";
                element.ShortCutKey = "Back";
                element.IsShowMessage = false;
                element.CanExecute = () => _models.BookHistoryCommand.CanPrevHistory();
                element.Execute = (s, e) => _models.BookHistoryCommand.PrevHistory();
                _elements[CommandType.PrevHistory] = element;
            }
            // NextHistory
            {
                var element = new CommandElement();
                element.Group = "移動";
                element.Text = "次の履歴へ進む";
                element.Note = "次の新しい履歴のブックを読み込みます";
                element.ShortCutKey = "Shift+Back";
                element.IsShowMessage = false;
                element.CanExecute = () => _models.BookHistoryCommand.CanNextHistory();
                element.Execute = (s, e) => _models.BookHistoryCommand.NextHistory();
                _elements[CommandType.NextHistory] = element;
            }

            // ToggleMediaPlay
            {
                var element = new CommandElement();
                element.Group = "動画";
                element.Text = "動画再生/停止";
                element.Note = "動画の再生と停止を切り替えます";
                element.IsShowMessage = false;
                element.CanExecute = () => _book.Book != null && _book.Book.IsMedia;
                element.Execute = (s, e) => _models.BookOperation.ToggleMediaPlay();
                _elements[CommandType.ToggleMediaPlay] = element;
            }

            // ToggleFolderOrder
            {
                var element = new CommandElement();
                element.Group = "ブック列";
                element.Text = "ブックの並び順を切り替える";
                element.Note = "ブックの並び順を順番に切り替えます";
                element.Execute = (s, e) => _models.FolderList.ToggleFolderOrder();
                element.ExecuteMessage = e => _models.FolderList.GetFolderOrder().GetToggle().ToAliasName();
                element.IsShowMessage = true;
                _elements[CommandType.ToggleFolderOrder] = element;
            }
            // SetFolderOrderByFileNameA
            {
                var element = new CommandElement();
                element.Group = "ブック列";
                element.Text = "ブック列はファイル名順昇順";
                element.Note = "ブックの並びを名前順(昇順)にします";
                element.Execute = (s, e) => _models.FolderList.SetFolderOrder(FolderOrder.FileName);
                element.CreateIsCheckedBinding = () => BindingGenerator.FolderOrder(FolderOrder.FileName);
                element.IsShowMessage = true;
                _elements[CommandType.SetFolderOrderByFileNameA] = element;
            }
            // SetFolderOrderByFileNameD
            {
                var element = new CommandElement();
                element.Group = "ブック列";
                element.Text = "ブック列はファイル名順降順";
                element.Note = "ブックの並びを名前順(降順)にします";
                element.Execute = (s, e) => _models.FolderList.SetFolderOrder(FolderOrder.FileNameDescending);
                element.CreateIsCheckedBinding = () => BindingGenerator.FolderOrder(FolderOrder.FileNameDescending);
                element.IsShowMessage = true;
                _elements[CommandType.SetFolderOrderByFileNameD] = element;
            }
            // SetFolderOrderByTimeStampA
            {
                var element = new CommandElement();
                element.Group = "ブック列";
                element.Text = "ブック列は日付昇順";
                element.Note = "ブックの並びを日付順(昇順)にします";
                element.Execute = (s, e) => _models.FolderList.SetFolderOrder(FolderOrder.TimeStamp);
                element.CreateIsCheckedBinding = () => BindingGenerator.FolderOrder(FolderOrder.TimeStamp);
                element.IsShowMessage = true;
                _elements[CommandType.SetFolderOrderByTimeStampA] = element;
            }
            // SetFolderOrderByTimeStampD
            {
                var element = new CommandElement();
                element.Group = "ブック列";
                element.Text = "ブック列は日付降順";
                element.Note = "ブックの並びを日付順(降順)にします";
                element.Execute = (s, e) => _models.FolderList.SetFolderOrder(FolderOrder.TimeStampDescending);
                element.CreateIsCheckedBinding = () => BindingGenerator.FolderOrder(FolderOrder.TimeStampDescending);
                element.IsShowMessage = true;
                _elements[CommandType.SetFolderOrderByTimeStampD] = element;
            }
            // SetFolderOrderBySizeA
            {
                var element = new CommandElement();
                element.Group = "ブック列";
                element.Text = "ブック列はサイズ昇順";
                element.Note = "ブックの並びをサイズ順(昇順)にします";
                element.Execute = (s, e) => _models.FolderList.SetFolderOrder(FolderOrder.Size);
                element.CreateIsCheckedBinding = () => BindingGenerator.FolderOrder(FolderOrder.Size);
                element.IsShowMessage = true;
                _elements[CommandType.SetFolderOrderBySizeA] = element;
            }
            // SetFolderOrderBySizeD
            {
                var element = new CommandElement();
                element.Group = "ブック列";
                element.Text = "ブック列はサイズ降順";
                element.Note = "ブックの並びをサイズ順(降順)にします";
                element.Execute = (s, e) => _models.FolderList.SetFolderOrder(FolderOrder.SizeDescending);
                element.CreateIsCheckedBinding = () => BindingGenerator.FolderOrder(FolderOrder.SizeDescending);
                element.IsShowMessage = true;
                _elements[CommandType.SetFolderOrderBySizeD] = element;
            }
            // SetFolderOrderByRandom
            {
                var element = new CommandElement();
                element.Group = "ブック列";
                element.Text = "ブック列はシャッフル";
                element.Note = "ブックの並びをシャッフルします";
                element.Execute = (s, e) => _models.FolderList.SetFolderOrder(FolderOrder.Random);
                element.CreateIsCheckedBinding = () => BindingGenerator.FolderOrder(FolderOrder.Random);
                element.IsShowMessage = true;
                _elements[CommandType.SetFolderOrderByRandom] = element;
            }

            // TogglePageMode
            {
                var element = new CommandElement();
                element.Group = "ページ表示";
                element.Text = "ページ表示モードを切り替える";
                element.Note = "1ページ表示/2ページ表示を切り替えます";
                element.CanExecute = () => true;
                element.Execute = (s, e) => _models.BookSetting.TogglePageMode();
                element.ExecuteMessage = e => _models.BookSetting.BookMemento.PageMode.GetToggle().ToAliasName();
                element.IsShowMessage = true;
                _elements[CommandType.TogglePageMode] = element;
            }
            // SetPageMode1
            {
                var element = new CommandElement();
                element.Group = "ページ表示";
                element.Text = "1ページ表示";
                element.Note = "1ページ表示にします";
                element.ShortCutKey = "Ctrl+1";
                element.MouseGesture = "RU";
                element.Execute = (s, e) => _models.BookSetting.SetPageMode(PageMode.SinglePage);
                element.CreateIsCheckedBinding = () => BindingGenerator.PageMode(PageMode.SinglePage);
                element.IsShowMessage = true;
                _elements[CommandType.SetPageMode1] = element;
            }
            // SetPageMode2
            {
                var element = new CommandElement();
                element.Group = "ページ表示";
                element.Text = "2ページ表示";
                element.Note = "2ページ表示にします";
                element.ShortCutKey = "Ctrl+2";
                element.MouseGesture = "RD";
                element.Execute = (s, e) => _models.BookSetting.SetPageMode(PageMode.WidePage);
                element.CreateIsCheckedBinding = () => BindingGenerator.PageMode(PageMode.WidePage);
                element.IsShowMessage = true;
                _elements[CommandType.SetPageMode2] = element;
            }
            // ToggleBookReadOrder
            {
                var element = new CommandElement();
                element.Group = "ページ表示";
                element.Text = "右開き、左開きを切り替える";
                element.Note = "右開き、左開きを切り替えます";
                element.CanExecute = () => true;
                element.Execute = (s, e) => _models.BookSetting.ToggleBookReadOrder();
                element.ExecuteMessage = e => _models.BookSetting.BookMemento.BookReadOrder.GetToggle().ToAliasName();
                element.IsShowMessage = true;
                _elements[CommandType.ToggleBookReadOrder] = element;
            }
            // SetBookReadOrderRight
            {
                var element = new CommandElement();
                element.Group = "ページ表示";
                element.Text = "右開き";
                element.Note = "読み進む方向を右開きにします。2ページ表示のときに若いページが右になります";
                element.Execute = (s, e) => _models.BookSetting.SetBookReadOrder(PageReadOrder.RightToLeft);
                element.CreateIsCheckedBinding = () => BindingGenerator.BookReadOrder(PageReadOrder.RightToLeft);
                element.IsShowMessage = true;
                _elements[CommandType.SetBookReadOrderRight] = element;
            }
            // SetBookReadOrderLeft
            {
                var element = new CommandElement();
                element.Group = "ページ表示";
                element.Text = "左開き";
                element.Note = "読み進む方向を左開きにします。2ページ表示のときに若いページが左になります";
                element.Execute = (s, e) => _models.BookSetting.SetBookReadOrder(PageReadOrder.LeftToRight);
                element.CreateIsCheckedBinding = () => BindingGenerator.BookReadOrder(PageReadOrder.LeftToRight);
                element.IsShowMessage = true;
                _elements[CommandType.SetBookReadOrderLeft] = element;
            }

            // ToggleIsSupportedDividePage
            {
                var element = new CommandElement();
                element.Group = "ページ表示";
                element.Text = "横長ページを分割する";
                element.Note = "1ページ表示時、横長ページを分割してページにします";
                element.Execute = (s, e) => _models.BookSetting.ToggleIsSupportedDividePage();
                element.ExecuteMessage = e => _models.BookSetting.BookMemento.IsSupportedDividePage ? "横長ページの区別をしない" : "横長ページを分割する";
                element.CanExecute = () => _models.BookSetting.CanPageModeSubSetting(PageMode.SinglePage);
                element.CreateIsCheckedBinding = () => BindingGenerator.BindingBookSetting(nameof(_models.BookSetting.BookMemento.IsSupportedDividePage));
                element.IsShowMessage = true;
                _elements[CommandType.ToggleIsSupportedDividePage] = element;
            }

            // ToggleIsSupportedWidePage
            {
                var element = new CommandElement();
                element.Group = "ページ表示";
                element.Text = "横長ページを2ページとみなす";
                element.Note = " 2ページ表示時、横長の画像を2ページ分とみなして単独表示します";
                element.Execute = (s, e) => _models.BookSetting.ToggleIsSupportedWidePage();
                element.ExecuteMessage = e => _models.BookSetting.BookMemento.IsSupportedWidePage ? "横長ページの区別をしない" : "横長ページを2ページとみなす";
                element.CanExecute = () => _models.BookSetting.CanPageModeSubSetting(PageMode.WidePage);
                element.CreateIsCheckedBinding = () => BindingGenerator.BindingBookSetting(nameof(_models.BookSetting.BookMemento.IsSupportedWidePage));
                element.IsShowMessage = true;
                _elements[CommandType.ToggleIsSupportedWidePage] = element;
            }
            // ToggleIsSupportedSingleFirstPage
            {
                var element = new CommandElement();
                element.Group = "ページ表示";
                element.Text = "最初のページを単独表示";
                element.Note = "2ページ表示でも最初のページは1ページ表示にします";
                element.Execute = (s, e) => _models.BookSetting.ToggleIsSupportedSingleFirstPage();
                element.ExecuteMessage = e => _models.BookSetting.BookMemento.IsSupportedSingleFirstPage ? "最初のページを区別しない" : "最初のページを単独表示";
                element.CanExecute = () => _models.BookSetting.CanPageModeSubSetting(PageMode.WidePage);
                element.CreateIsCheckedBinding = () => BindingGenerator.BindingBookSetting(nameof(_models.BookSetting.BookMemento.IsSupportedSingleFirstPage));
                element.IsShowMessage = true;
                _elements[CommandType.ToggleIsSupportedSingleFirstPage] = element;
            }
            // ToggleIsSupportedSingleLastPage
            {
                var element = new CommandElement();
                element.Group = "ページ表示";
                element.Text = "最後のページを単独表示";
                element.Note = "2ページ表示でも最後のページは1ページ表示にします";
                element.Execute = (s, e) => _models.BookSetting.ToggleIsSupportedSingleLastPage();
                element.ExecuteMessage = e => _models.BookSetting.BookMemento.IsSupportedSingleLastPage ? "最後のページを区別しない" : "最後のページを単独表示";
                element.CanExecute = () => _models.BookSetting.CanPageModeSubSetting(PageMode.WidePage);
                element.CreateIsCheckedBinding = () => BindingGenerator.BindingBookSetting(nameof(_models.BookSetting.BookMemento.IsSupportedSingleLastPage));
                element.IsShowMessage = true;
                _elements[CommandType.ToggleIsSupportedSingleLastPage] = element;
            }

            // ToggleIsRecursiveFolder
            {
                var element = new CommandElement();
                element.Group = "ページ表示";
                element.Text = "サブフォルダーを読み込む";
                element.Note = "フォルダーから画像を読み込むときにサブフォルダーまたは圧縮ファイルも同時に読み込みます";
                element.Execute = (s, e) => _models.BookSetting.ToggleIsRecursiveFolder();
                element.ExecuteMessage = e => _models.BookSetting.BookMemento.IsRecursiveFolder ? "サブフォルダーは読み込まない" : "サブフォルダーも読み込む";
                element.CreateIsCheckedBinding = () => BindingGenerator.BindingBookSetting(nameof(_models.BookSetting.BookMemento.IsRecursiveFolder));
                element.IsShowMessage = true;
                _elements[CommandType.ToggleIsRecursiveFolder] = element;
            }

            // ToggleSortMode
            {
                var element = new CommandElement();
                element.Group = "ページ列";
                element.Text = "ページの並び順を切り替える";
                element.Note = "ページの並び順を順番に切り替えます";
                element.CanExecute = () => true;
                element.Execute = (s, e) => _models.BookSetting.ToggleSortMode();
                element.ExecuteMessage = e => _models.BookSetting.BookMemento.SortMode.GetToggle().ToAliasName();
                element.IsShowMessage = true;
                _elements[CommandType.ToggleSortMode] = element;
            }
            // SetSortModeFileName
            {
                var element = new CommandElement();
                element.Group = "ページ列";
                element.Text = "ファイル名昇順";
                element.Note = "ページの並び順をファイル名昇順にします";
                element.Execute = (s, e) => _models.BookSetting.SetSortMode(PageSortMode.FileName);
                element.CreateIsCheckedBinding = () => BindingGenerator.SortMode(PageSortMode.FileName);
                element.IsShowMessage = true;
                _elements[CommandType.SetSortModeFileName] = element;
            }
            // SetSortModeFileNameDescending
            {
                var element = new CommandElement();
                element.Group = "ページ列";
                element.Text = "ファイル名降順";
                element.Note = "ページの並び順をファイル名降順にします";
                element.Execute = (s, e) => _models.BookSetting.SetSortMode(PageSortMode.FileNameDescending);
                element.CreateIsCheckedBinding = () => BindingGenerator.SortMode(PageSortMode.FileNameDescending);
                element.IsShowMessage = true;
                _elements[CommandType.SetSortModeFileNameDescending] = element;
            }
            // SetSortModeTimeStamp
            {
                var element = new CommandElement();
                element.Group = "ページ列";
                element.Text = "ファイル日付昇順";
                element.Note = "ページの並び順をファイル日付昇順にします";
                element.Execute = (s, e) => _models.BookSetting.SetSortMode(PageSortMode.TimeStamp);
                element.CreateIsCheckedBinding = () => BindingGenerator.SortMode(PageSortMode.TimeStamp);
                element.IsShowMessage = true;
                _elements[CommandType.SetSortModeTimeStamp] = element;
            }
            // SetSortModeTimeStampDescending
            {
                var element = new CommandElement();
                element.Group = "ページ列";
                element.Text = "ファイル日付降順";
                element.Note = "ページの並び順をファイル日付降順にします";
                element.Execute = (s, e) => _models.BookSetting.SetSortMode(PageSortMode.TimeStampDescending);
                element.CreateIsCheckedBinding = () => BindingGenerator.SortMode(PageSortMode.TimeStampDescending);
                element.IsShowMessage = true;
                _elements[CommandType.SetSortModeTimeStampDescending] = element;
            }
            // SetSortModeSize
            {
                var element = new CommandElement();
                element.Group = "ページ列";
                element.Text = "ファイルサイズ昇順";
                element.Note = "ページの並び順をファイルサイズ昇順にします";
                element.Execute = (s, e) => _models.BookSetting.SetSortMode(PageSortMode.Size);
                element.CreateIsCheckedBinding = () => BindingGenerator.SortMode(PageSortMode.Size);
                element.IsShowMessage = true;
                _elements[CommandType.SetSortModeSize] = element;
            }
            // SetSortModeSizeDescending
            {
                var element = new CommandElement();
                element.Group = "ページ列";
                element.Text = "ファイルサイズ降順";
                element.Note = "ページの並び順をファイルサイズ降順にします";
                element.Execute = (s, e) => _models.BookSetting.SetSortMode(PageSortMode.SizeDescending);
                element.CreateIsCheckedBinding = () => BindingGenerator.SortMode(PageSortMode.SizeDescending);
                element.IsShowMessage = true;
                _elements[CommandType.SetSortModeSizeDescending] = element;
            }
            // SetSortModeRandom
            {
                var element = new CommandElement();
                element.Group = "ページ列";
                element.Text = "シャッフル";
                element.Note = "ページの並び順をシャッフルます";
                element.Execute = (s, e) => _models.BookSetting.SetSortMode(PageSortMode.Random);
                element.CreateIsCheckedBinding = () => BindingGenerator.SortMode(PageSortMode.Random);
                element.IsShowMessage = true;
                _elements[CommandType.SetSortModeRandom] = element;
            }

            // SetDefaultPageSetting
            {
                var element = new CommandElement();
                element.Group = "ページ表示";
                element.Text = "ページ設定の初期化";
                element.Note = "既定のページ設定に戻します";
                element.Execute = (s, e) => _models.BookSetting.SetDefaultPageSetting();
                element.IsShowMessage = true;
                _elements[CommandType.SetDefaultPageSetting] = element;
            }


            // ToggleBookmark
            {
                var element = new CommandElement();
                element.Group = "ブックマーク";
                element.Text = "ブックマーク登録/解除";
                element.MenuText = "ブックマーク";
                element.Note = "現在開いているブックのブックマークの登録/解除を切り替えます";
                element.Execute = (s, e) => _models.BookOperation.ToggleBookmark();
                element.CanExecute = () => _models.BookOperation.CanBookmark();
                element.ExecuteMessage = e => _models.BookOperation.IsBookmark ? "ブックマーク解除" : "ブックマークに登録";
                element.IsShowMessage = true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(_models.BookOperation.IsBookmark)) { Source = _models.BookOperation, Mode = BindingMode.OneWay };
                element.ShortCutKey = "Ctrl+D";
                _elements[CommandType.ToggleBookmark] = element;
            }

            // PrevBookmark
            {
                var element = new CommandElement();
                element.Group = "ブックマーク";
                element.Text = "前のブックマークに移動";
                element.Note = "ブックマークリスト順で前のブックに移動します";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.BookmarkList.PrevBookmark();
                _elements[CommandType.PrevBookmark] = element;
            }
            // NextBookmark
            {
                var element = new CommandElement();
                element.Group = "ブックマーク";
                element.Text = "次のブックマークへ移動";
                element.Note = "ブックマークリスト順で次のブックに移動します";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.BookmarkList.NextBookmark();
                _elements[CommandType.NextBookmark] = element;
            }

            // TogglePagemark
            {
                var element = new CommandElement();
                element.Group = "ページマーク";
                element.Text = "ページマーク登録/解除";
                element.MenuText = "ページマーク";
                element.Note = "現在開いているページのページマークの登録/解除を切り替えます";
                element.Execute = (s, e) => _models.BookOperation.TogglePagemark();
                element.CanExecute = () => _models.BookOperation.CanPagemark();
                element.ExecuteMessage = e => _models.BookOperation.IsMarked() ? "ページマーク解除" : "ページマーク登録";
                element.IsShowMessage = true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(_models.BookOperation.IsPagemark)) { Source = _models.BookOperation, Mode = BindingMode.OneWay };
                element.ShortCutKey = "Ctrl+M";
                _elements[CommandType.TogglePagemark] = element;
            }

            // PrevPagemark
            {
                var element = new CommandElement();
                element.Group = "ページマーク";
                element.Text = "前のページマークに移動";
                element.Note = "前のページマークに移動します";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.PagemarkList.PrevPagemark();
                _elements[CommandType.PrevPagemark] = element;
            }
            // NextPagemark
            {
                var element = new CommandElement();
                element.Group = "ページマーク";
                element.Text = "次のページマークへ移動";
                element.Note = "次のページマークへ移動します";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.PagemarkList.NextPagemark();
                _elements[CommandType.NextPagemark] = element;
            }

            // PrevPagemarkInBook
            {
                var element = new CommandElement();
                element.Group = "ページマーク";
                element.Text = "ブック内の前のページマークに移動";
                element.Note = "現在のブック内で前のページマークに移動します";
                element.IsShowMessage = false;
                element.CanExecute = () => _models.BookOperation.CanPrevPagemarkInPlace((MovePagemarkCommandParameter)element.Parameter);
                element.Execute = (s, e) => _models.BookOperation.PrevPagemarkInPlace((MovePagemarkCommandParameter)element.Parameter);
                element.DefaultParameter = new MovePagemarkCommandParameter();
                _elements[CommandType.PrevPagemarkInBook] = element;
            }
            // NextPagemarkInBook
            {
                var element = new CommandElement();
                element.Group = "ページマーク";
                element.Text = "ブック内の次のページマークへ移動";
                element.Note = "現在のブック内で次のページマークへ移動します";
                element.IsShowMessage = false;
                element.CanExecute = () => _models.BookOperation.CanNextPagemarkInPlace((MovePagemarkCommandParameter)element.Parameter);
                element.Execute = (s, e) => _models.BookOperation.NextPagemarkInPlace((MovePagemarkCommandParameter)element.Parameter);
                element.DefaultParameter = new ShareCommandParameter() { CommandType = CommandType.PrevPagemarkInBook };
                _elements[CommandType.NextPagemarkInBook] = element;
            }


            // ToggleCustomSize
            {
                var element = new CommandElement();
                element.Group = "表示サイズ";
                element.Text = "サイズ指定のON /OFF";
                element.MenuText = "サイズ指定";
                element.Note = "オリジナルサイズに適用されるサイズ指定の有効/無効を切り替えます";
                element.CanExecute = () => true;
                element.IsShowMessage = true;
                element.ExecuteMessage = e => _models.PictureProfile.CustomSize.IsEnabled ? "サイズ指定OFF" : "サイズ指定ON";
                element.Execute = (s, e) => _models.PictureProfile.CustomSize.IsEnabled = !_models.PictureProfile.CustomSize.IsEnabled;
                element.CreateIsCheckedBinding = () => new Binding(nameof(_models.PictureProfile.CustomSize.IsEnabled)) { Mode = BindingMode.OneWay, Source = _models.PictureProfile.CustomSize };
                _elements[CommandType.ToggleCustomSize] = element;
            }


            // ToggleResizeFilter
            {
                var element = new CommandElement();
                element.Group = "エフェクト";
                element.Text = "リサイズフィルターON /OFF";
                element.MenuText = "リサイズフィルター";
                element.Note = "リサイズフィルターの有効/無効を切り替えます";
                element.CanExecute = () => true;
                element.ShortCutKey = "Ctrl+R";
                element.IsShowMessage = true;
                element.ExecuteMessage = e => _models.PictureProfile.IsResizeFilterEnabled ? "リサイズフィルターOFF" : "リサイズフィルターON";
                element.Execute = (s, e) => _models.PictureProfile.IsResizeFilterEnabled = !_models.PictureProfile.IsResizeFilterEnabled;
                element.CreateIsCheckedBinding = () => new Binding(nameof(_models.PictureProfile.IsResizeFilterEnabled)) { Mode = BindingMode.OneWay, Source = _models.PictureProfile };
                _elements[CommandType.ToggleResizeFilter] = element;
            }

            // ToggleEffect
            {
                var element = new CommandElement();
                element.Group = "エフェクト";
                element.Text = "エフェクトON /OFF";
                element.MenuText = "エフェクト";
                element.Note = "エフェクトの有効/無効を切り替えます";
                element.CanExecute = () => true;
                element.ShortCutKey = "Ctrl+E";
                element.IsShowMessage = true;
                element.ExecuteMessage = e => _models.ImageEffect.IsEnabled ? "エフェクトOFF" : "エフェクトON";
                element.Execute = (s, e) => _models.ImageEffect.IsEnabled = !_models.ImageEffect.IsEnabled;
                element.CreateIsCheckedBinding = () => new Binding(nameof(_models.ImageEffect.IsEnabled)) { Mode = BindingMode.OneWay, Source = _models.ImageEffect };
                _elements[CommandType.ToggleEffect] = element;
            }


            // ToggleIsLoupe
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "ルーペON/OFF";
                element.MenuText = "ルーペ";
                element.Note = "ルーペの有効/無効を切り替えます";
                element.CanExecute = () => true;
                element.IsShowMessage = false;
                element.ExecuteMessage = e => _models.MouseInput.IsLoupeMode ? "ルーペOFF" : "ルーペON";
                element.Execute = (s, e) => _models.MouseInput.IsLoupeMode = !_models.MouseInput.IsLoupeMode;
                element.CreateIsCheckedBinding = () => new Binding(nameof(_models.MouseInput.IsLoupeMode)) { Mode = BindingMode.OneWay, Source = _models.MouseInput };
                _elements[CommandType.ToggleIsLoupe] = element;
            }

            // LoupeOn
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "ルーペON";
                element.MenuText = "ルーペON";
                element.Note = "ルーペモードにする";
                element.CanExecute = () => true;
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.MouseInput.IsLoupeMode = true;
                _elements[CommandType.LoupeOn] = element;
            }

            // LoupeOff
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "ルーペOFF";
                element.MenuText = "ルーペOFF";
                element.Note = "ルーペモードを解除する";
                element.CanExecute = () => true;
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.MouseInput.IsLoupeMode = false;
                _elements[CommandType.LoupeOff] = element;
            }

            // LoupeScaleUp
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "ルーペ倍率拡大";
                element.Note = "ルーペ倍率を拡大します。ルーペ使用時のみ機能します。";
                element.CanExecute = () => _models.MouseInput.IsLoupeMode;
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.MouseInput.Loupe.LoupeZoomIn();
                _elements[CommandType.LoupeScaleUp] = element;
            }

            // LoupeScaleDown
            {
                var element = new CommandElement();
                element.Group = "ビュー操作";
                element.Text = "ルーペ倍率縮小";
                element.Note = "ルーペ倍率を縮小します。ルーペ使用時のみ機能します。";
                element.CanExecute = () => _models.MouseInput.IsLoupeMode;
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.MouseInput.Loupe.LoupeZoomOut();
                _elements[CommandType.LoupeScaleDown] = element;
            }

            // OpenSettingWindow
            {
                var element = new CommandElement();
                element.Group = "その他";
                element.Text = "設定ウィンドウを開く";
                element.MenuText = "設定(_O)...";
                element.Note = "設定ウィンドウを開きます";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.MainWindowModel.OpenSettingWindow();
                _elements[CommandType.OpenSettingWindow] = element;
            }
            // OpenSettingFilesFolder
            {
                var element = new CommandElement();
                element.Group = "その他";
                element.Text = "設定ファイルの場所を開く";
                element.Note = "設定ファイルが保存されているフォルダーを開きます";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.MainWindowModel.OpenSettingFilesFolder();
                _elements[CommandType.OpenSettingFilesFolder] = element;
            }

            // OpenVersionWindow
            {
                var element = new CommandElement();
                element.Group = "その他";
                element.Text = "バージョン情報を表示する";
                element.MenuText = "このアプリについて(_A)...";
                element.Note = "バージョン情報を表示します";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.MainWindowModel.OpenVersionWindow();
                _elements[CommandType.OpenVersionWindow] = element;
            }
            // CloseApplication
            {
                var element = new CommandElement();
                element.Group = "その他";
                element.Text = "アプリを終了する";
                element.MenuText = "終了(_X)";
                element.Note = "このアプリケーションを終了させます";
                element.IsShowMessage = false;
                element.CanExecute = () => true;
                _elements[CommandType.CloseApplication] = element;
            }


            // TogglePermitFileCommand
            {
                var element = new CommandElement();
                element.Group = "その他";
                element.Text = "ファイル操作有効/無効";
                element.MenuText = "ファイル操作許可(_P)";
                element.Note = "ファイル操作系コマンドの有効/無効を切り替えます";
                element.IsShowMessage = true;
                element.Execute = (s, e) => _models.FileIOProfile.IsEnabled = !_models.FileIOProfile.IsEnabled;
                element.ExecuteMessage = e => _models.FileIOProfile.IsEnabled ? "ファイル操作無効" : "ファイル操作有効";
                element.CanExecute = () => true;
                element.CreateIsCheckedBinding = () => new Binding(nameof(_models.FileIOProfile.IsEnabled)) { Source = _models.FileIOProfile, Mode = BindingMode.OneWay };
                _elements[CommandType.TogglePermitFileCommand] = element;
            }


            // HelpOnline
            {
                var element = new CommandElement();
                element.Group = "その他";
                element.Text = "オンラインヘルプ";
                element.MenuText = "オンラインヘルプ";
                element.Note = "オンラインヘルプを表示します";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.MainWindowModel.OpenOnlineHelp();
                element.CanExecute = () => App.Current.IsNetworkEnabled;
                _elements[CommandType.HelpOnline] = element;
            }

            // HelpCommandList
            {
                var element = new CommandElement();
                element.Group = "その他";
                element.Text = "コマンドのヘルプを表示する";
                element.MenuText = "コマンドヘルプ";
                element.Note = "全コマンドのヘルプをブラウザで表示します";
                element.IsShowMessage = false;
                element.Execute = (s, e) => this.OpenCommandListHelp();
                element.CanExecute = () => true;
                _elements[CommandType.HelpCommandList] = element;
            }

            // HelpMainMenu
            {
                var element = new CommandElement();
                element.Group = "その他";
                element.Text = "メインメニューのヘルプを表示する";
                element.MenuText = "メインメニューヘルプ";
                element.Note = "メインメニューのヘルプをブラウザで表示します";
                element.IsShowMessage = false;
                element.Execute = (s, e) => _models.MenuBar.OpenMainMenuHelp();
                element.CanExecute = () => true;
                _elements[CommandType.HelpMainMenu] = element;
            }

            // OpenContextMenu
            {
                var element = new CommandElement();
                element.Group = "その他";
                element.Text = "コンテキストメニューを開く";
                element.Note = "コンテキストメニューを開きます";
                element.IsShowMessage = false;
                element.CanExecute = () => true;
                _elements[CommandType.OpenContextMenu] = element;
            }


            // ExportBackup
            {
                var element = new CommandElement();
                element.Group = "その他";
                element.Text = "全設定をエクスポート";
                element.MenuText = "全設定をエクスポート...";
                element.Note = "設定、履歴、ブックマーク、ページマークのバックアップを作成します。サムネイルキャッシュはバックアップされません";
                element.IsShowMessage = false;
                element.Execute = (s, e) => SaveData.Current.ExportBackup();
                _elements[CommandType.ExportBackup] = element;
            }

            // ImportBackup
            {
                var element = new CommandElement();
                element.Group = "その他";
                element.Text = "全設定をインポート";
                element.MenuText = "全設定をインポート...";
                element.Note = "バックアップファイルから復元項目を選んで復元します。";
                element.IsShowMessage = false;
                element.Execute = (s, e) => SaveData.Current.ImportBackup();
                _elements[CommandType.ImportBackup] = element;
            }

            // 無効な命令にダミー設定
            foreach (var ignore in CommandTypeExtensions.IgnoreCommandTypes)
            {
                var element = new CommandElement();
                element.Group = "dummy";
                element.Text = "dummy";
                element.Execute = (s, e) => { return; };
                _elements[ignore] = element;
            }

            // 並び替え
            //_Elements = _Elements.OrderBy(e => e.Key).ToDictionary(e => e.Key, e => e.Value);

            // デフォルト設定として記憶
            s_defaultMemento = CreateMemento();
        }

        #endregion

        #region Memento

        [DataContract]
        public class Memento
        {
            [DataMember]
            public int _Version { get; set; } = Config.Current.ProductVersionNumber;

            // V2: Enum型キーは前方互換性に難があるため、文字列化して保存する

            [Obsolete, DataMember(Name = "Elements", EmitDefaultValue = false)]
            private Dictionary<CommandType, CommandElement.Memento> _elementsV1;

            [DataMember(Name = "ElementsV2")]
            private Dictionary<string, CommandElement.Memento> _elementsV2;

            [DataMember, DefaultValue(true)]
            public bool IsReversePageMove { get; set; }

            [DataMember]
            public bool IsReversePageMoveWheel { get; set; }

            public Dictionary<CommandType, CommandElement.Memento> Elements { get; set; } = new Dictionary<CommandType, CommandElement.Memento>();


            [OnSerializing]
            internal void OnSerializing(StreamingContext context)
            {
                _elementsV2 = Elements.ToDictionary(e => e.Key.ToString(), e => e.Value);
            }

            [OnDeserializing]
            private void OnDeserializing(StreamingContext c)
            {
                this.InitializePropertyDefaultValues();
            }

            [OnDeserialized]
            internal void OnDeserialized(StreamingContext context)
            {
                Elements = new Dictionary<CommandType, CommandElement.Memento>();

#pragma warning disable CS0612
                if (_elementsV1 != null)
                {
                    Elements = _elementsV1;
                    _elementsV1 = null;
                }
#pragma warning restore CS0612

                if (_elementsV2 != null)
                {
                    foreach (var element in _elementsV2)
                    {
                        if (Enum.TryParse(element.Key, out CommandType key))
                        {
                            Elements[key] = element.Value;
                        }
                    }
                    _elementsV2 = null;
                }
            }

            public Memento Clone()
            {
                var memento = (Memento)this.MemberwiseClone();
                memento.Elements = this.Elements.ToDictionary(e => e.Key, e => e.Value.Clone());
                return memento;
            }
        }

        //
        public Memento CreateMemento()
        {
            var memento = new Memento();

            foreach (var pair in _elements)
            {
                if (pair.Key.IsDisable()) continue;
                memento.Elements.Add(pair.Key, pair.Value.CreateMemento());
            }

            memento.IsReversePageMove = this.IsReversePageMove;
            memento.IsReversePageMoveWheel = this.IsReversePageMoveWheel;

            return memento;
        }

        //
        public void Restore(Memento memento, bool onHold)
        {
            RestoreInner(memento);
            Changed?.Invoke(this, new CommandChangedEventArgs(onHold));
        }

        //
        private void RestoreInner(Memento memento)
        {
            if (memento == null) return;

            foreach (var pair in memento.Elements)
            {
                if (_elements.ContainsKey(pair.Key))
                {
                    _elements[pair.Key].Restore(pair.Value);
                }
            }

            this.IsReversePageMove = memento.IsReversePageMove;
            this.IsReversePageMoveWheel = memento.IsReversePageMoveWheel;


#pragma warning disable CS0612
            // ToggleStrechModeの復元(1.14互換用)
            if (_elements[CommandType.ToggleStretchMode].IsToggled)
            {
                var flags = ((ToggleStretchModeCommandParameter)_elements[CommandType.ToggleStretchMode].Parameter).StretchModes;

                Dictionary<PageStretchMode, CommandType> _CommandTable = new Dictionary<PageStretchMode, CommandType>
                {
                    [PageStretchMode.None] = CommandType.SetStretchModeNone,
                    [PageStretchMode.Inside] = CommandType.SetStretchModeInside,
                    [PageStretchMode.Outside] = CommandType.SetStretchModeOutside,
                    [PageStretchMode.Uniform] = CommandType.SetStretchModeUniform,
                    [PageStretchMode.UniformToFill] = CommandType.SetStretchModeUniformToFill,
                    [PageStretchMode.UniformToSize] = CommandType.SetStretchModeUniformToSize,
                    [PageStretchMode.UniformToVertical] = CommandType.SetStretchModeUniformToVertical,
                };

                foreach (var item in _CommandTable)
                {
                    flags[item.Key] = _elements[item.Value].IsToggled;
                }
            }
#pragma warning restore CS0612

            // compatible before ver.29
            if (memento._Version < Config.GenerateProductVersionNumber(1, 29, 0))
            {
                // ver.29以前はデフォルトOFF
                this.IsReversePageMove = false;
            }
        }

        #endregion
    }
}
