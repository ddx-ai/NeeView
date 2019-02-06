﻿using NeeLaboratory.ComponentModel;
using NeeView.Windows.Property;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace NeeView
{
    // プログラムの種類
    public enum ExternalProgramType
    {
        [AliasName("@EnumExternalProgramTypeNormal")]
        Normal,

        [AliasName("@EnumExternalProgramTypeProtocol")]
        Protocol,
    }

    // 複数ページのときの動作
    public enum MultiPageOptionType
    {
        [AliasName("@EnumMultiPageOptionTypeOnce")]
        Once,

        [AliasName("@EnumMultiPageOptionTypeTwice")]
        Twice,
    };

    // 圧縮ファイルの時の動作
    public enum ArchiveOptionType
    {
        [AliasName("@EnumArchiveOptionTypeNone")]
        None,

        [AliasName("@EnumArchiveOptionTypeSendArchiveFile")]
        SendArchiveFile,

        [AliasName("@EnumArchiveOptionTypeSendArchivePath")]
        SendArchivePath, // ver 33.0

        [AliasName("@EnumArchiveOptionTypeSendExtractFile")]
        SendExtractFile,
    }

    // コピー設定
    [DataContract]
    public class ClipboardUtility : BindableBase
    {
        private ArchiveOptionType _archiveOption;
        private string _archiveSeparater;

        // 複数ページのときの動作
        [DataMember]
        [PropertyMember("@ParamClipboardMultiPageOption")]
        public MultiPageOptionType MultiPageOption { get; set; }

        // 圧縮ファイルのときの動作
        [DataMember]
        [PropertyMember("@ParamClipboardArchiveOption")]
        public ArchiveOptionType ArchiveOption
        {
            get { return _archiveOption; }
            set { SetProperty(ref _archiveOption, value); }
        }

        [DataMember(EmitDefaultValue = false)]
        [PropertyMember("@ParamClipboardArchiveSeparater", EmptyMessage = "\\")]
        public string ArchiveSeparater
        {
            get => _archiveSeparater;
            set => _archiveSeparater = string.IsNullOrEmpty(value) ? null : value;
        }


        // コンストラクタ
        private void Constructor()
        {
            MultiPageOption = MultiPageOptionType.Once;
            ArchiveOption = ArchiveOptionType.SendExtractFile;
        }

        // コンストラクタ
        public ClipboardUtility()
        {
            Constructor();
        }

        //
        [OnDeserializing]
        private void Deserializing(StreamingContext c)
        {
            Constructor();
        }


        // クリップボードにコピー
        public void Copy(List<Page> pages)
        {
            var files = new List<string>();

            foreach (var page in pages)
            {
                // file
                if (page.Entry.IsFileSystem)
                {
                    files.Add(page.GetFilePlace());
                }
                // in archive
                else
                {
                    switch (ArchiveOption)
                    {
                        case ArchiveOptionType.None:
                            break;
                        case ArchiveOptionType.SendArchiveFile:
                            files.Add(page.GetFilePlace());
                            break;
                        case ArchiveOptionType.SendExtractFile:
                            files.Add(page.Content.CreateTempFile(true).Path);
                            break;
                        case ArchiveOptionType.SendArchivePath:
                            files.Add(page.Entry.CreateArchivePath(_archiveSeparater));
                            break;
                    }
                }
                if (MultiPageOption == MultiPageOptionType.Once || ArchiveOption == ArchiveOptionType.SendArchiveFile) break;
            }

            if (files.Count > 0)
            {
                var data = new System.Windows.DataObject();
                data.SetData(System.Windows.DataFormats.FileDrop, files.ToArray());
                data.SetData(System.Windows.DataFormats.UnicodeText, string.Join("\r\n", files));
                System.Windows.Clipboard.SetDataObject(data);
            }
        }

        // クリップボードに画像をコピー
        public static void CopyImage(System.Windows.Media.Imaging.BitmapSource image)
        {
            System.Windows.Clipboard.SetImage(image);
        }


        // クリップボードからペースト
        public void Paste()
        {
            var data = System.Windows.Clipboard.GetDataObject(); // クリップボードからオブジェクトを取得する。
            if (data.GetDataPresent(System.Windows.DataFormats.FileDrop)) // テキストデータかどうか確認する。
            {
                var files = (string[])data.GetData(System.Windows.DataFormats.FileDrop); // オブジェクトからテキストを取得する。
                Debug.WriteLine("=> " + files[0]);
            }
        }


        // インスタンスのクローン
        public ClipboardUtility Clone()
        {
            return (ClipboardUtility)MemberwiseClone();
        }
    }



    // 外部アプリ起動
    [DataContract]
    public class ExternalApplication : BindableBase
    {
        // コマンドパラメータで使用されるキーワード
        private const string _keyFile = "$File";
        private const string _keyUri = "$Uri";

        private ExternalProgramType _programType;
        private ArchiveOptionType _archiveOption;
        private string _archiveSeparater;

        /// <summary>
        /// ProgramType property.
        /// </summary>
        [DataMember]
        [PropertyMember("@ParamExternalProgramType")]
        public ExternalProgramType ProgramType
        {
            get { return _programType; }
            set { if (_programType != value) { _programType = value; RaisePropertyChanged(); } }
        }

        // コマンド
        [DataMember]
        [PropertyPath("@ParamExternalCommand", Tips = "@ParamExternalCommandTips", Filter = "EXE|*.exe|All|*.*")]
        public string Command { get; set; }

        // コマンドパラメータ
        // $FILE = 渡されるファイルパス
        [DataMember]
        [PropertyMember("@ParamExternalParameter")]
        public string Parameter { get; set; }

        // プロトコル
        [DataMember]
        [PropertyMember("@ParamExternalProtocol", Tips = "@ParamExternalProtocolTips")]
        public string Protocol { get; set; }

        // 複数ページのときの動作
        [DataMember]
        [PropertyMember("@ParamExternalMultiPageOption")]
        public MultiPageOptionType MultiPageOption { get; set; }

        // 圧縮ファイルのときの動作
        [DataMember]
        [PropertyMember("@ParamExternalArchiveOption")]
        public ArchiveOptionType ArchiveOption
        {
            get { return _archiveOption; }
            set { SetProperty(ref _archiveOption, value); }
        }

        [DataMember(EmitDefaultValue = false)]
        [PropertyMember("@ParamExternalArchiveSeparater", EmptyMessage = "\\")]
        public string ArchiveSeparater
        {
            get => _archiveSeparater;
            set => _archiveSeparater = string.IsNullOrEmpty(value) ? null : value;
        }

        // 拡張子に関連付けられたアプリを起動するかの判定
        public bool IsDefaultApplication => string.IsNullOrWhiteSpace(Command);

        // 最後に実行したコマンド
        public string LastCall { get; set; }

        // コマンドパラメータ文字列のバリデート
        public static string ValidateApplicationParam(string source)
        {
            if (source == null) source = "";
            source = source.Trim();
            return source.Contains(_keyFile) ? source : (source + $" \"{_keyFile}\"").Trim();
        }

        // コンストラクタ
        private void Constructor()
        {
            Parameter = "\"" + _keyFile + "\"";
            MultiPageOption = MultiPageOptionType.Once;
            ArchiveOption = ArchiveOptionType.SendExtractFile;
        }

        // コンストラクタ
        public ExternalApplication()
        {
            Constructor();
        }

        //
        [OnDeserializing]
        private void Deserializing(StreamingContext c)
        {
            Constructor();
        }

        [OnDeserialized]
        private void Deserialized(StreamingContext c)
        {
        }

        // 外部アプリの実行
        public void Call(List<Page> pages)
        {
            this.LastCall = null;

            foreach (var page in pages)
            {
                // file
                if (page.Entry.Archiver is FolderArchive)
                {
                    CallProcess(page.GetFilePlace());
                }
                // in archive
                else
                {
                    switch (ArchiveOption)
                    {
                        case ArchiveOptionType.None:
                            break;
                        case ArchiveOptionType.SendArchiveFile:
                            CallProcess(page.GetFolderOpenPlace());
                            break;
                        case ArchiveOptionType.SendExtractFile:
                            if (page.Entry.IsDirectory)
                            {
                                throw new ApplicationException(Properties.Resources.ExceptionNotSupportArchiveFolder);
                            }
                            else
                            {
                                CallProcess(page.Content.CreateTempFile(true).Path);
                            }
                            break;
                        case ArchiveOptionType.SendArchivePath:
                            CallProcess(page.Entry.CreateArchivePath(_archiveSeparater));
                            break;
                    }
                }
                if (MultiPageOption == MultiPageOptionType.Once || ArchiveOption == ArchiveOptionType.SendArchiveFile) break;
            }
        }

        // 外部アプリの実行(コア)
        private void CallProcess(string fileName)
        {
            switch (this.ProgramType)
            {
                case ExternalProgramType.Normal:
                    if (IsDefaultApplication)
                    {
                        this.LastCall = $"\"{fileName}\"";
                        Debug.WriteLine($"CallProcess: {LastCall}");
                        System.Diagnostics.Process.Start(fileName);
                    }
                    else
                    {
                        string param = ReplaceKeyword(this.Parameter, fileName);
                        this.LastCall = $"\"{Command}\" {param}";
                        Debug.WriteLine($"CallProcess: {LastCall}");
                        System.Diagnostics.Process.Start(Command, param);
                    }
                    return;

                case ExternalProgramType.Protocol:
                    if (!string.IsNullOrWhiteSpace(this.Protocol))
                    {
                        string protocol = ReplaceKeyword(this.Protocol, fileName);
                        this.LastCall = protocol;
                        Debug.WriteLine($"CallProcess: {LastCall}");
                        System.Diagnostics.Process.Start(protocol);
                    }
                    return;
            }
        }

        //
        private string ReplaceKeyword(string s, string filenName)
        {
            var uriData = Uri.EscapeDataString(filenName);

            s = s.Replace(_keyUri, uriData);
            s = s.Replace(_keyFile, filenName);
            return s;
        }


        // インスタンスのクローン
        public ExternalApplication Clone()
        {
            return (ExternalApplication)MemberwiseClone();
        }
    }
}
