﻿using NeeLaboratory.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NeeView
{
    /// <summary>
    /// マウス入力状態
    /// </summary>
    public enum MouseInputState
    {
        None,
        Normal,
        Loupe,
        Drag,
        Gesture,
    }

    /// <summary>
    /// MouseInputManager
    /// </summary>
    public class MouseInput : BindableBase
    {
        static MouseInput() => Current = new MouseInput();
        public static MouseInput Current { get; }

        //
        private FrameworkElement _sender;

        /// <summary>
        /// 状態：既定
        /// </summary>
        public MouseInputNormal Normal { get; private set; }

        /// <summary>
        /// 状態：ルーペ
        /// </summary>
        public MouseInputLoupe Loupe { get; private set; }

        /// <summary>
        /// 状態：ドラッグ
        /// </summary>
        public MouseInputDrag Drag { get; private set; }

        /// <summary>
        /// 状態：ジェスチャー
        /// </summary>
        public MouseInputGesture Gesture { get; private set; }

        /// <summary>
        /// 遷移テーブル
        /// </summary>
        private Dictionary<MouseInputState, MouseInputBase> _mouseInputCollection;

        /// <summary>
        /// 現在状態
        /// </summary>
        private MouseInputState _state;
        public MouseInputState State => _state;

        /// <summary>
        /// 現在状態（実体）
        /// </summary>
        private MouseInputBase _current;

        /// <summary>
        /// ボタン入力イベント
        /// </summary>
        public event EventHandler<MouseButtonEventArgs> MouseButtonChanged;

        /// <summary>
        /// ホイール入力イベント
        /// </summary>
        public event EventHandler<MouseWheelEventArgs> MouseWheelChanged;

        /// <summary>
        /// 一定距離カーソルが移動したイベント
        /// </summary>
        public event EventHandler MouseMoved;


        /// <summary>
        /// コマンド系イベントクリア
        /// </summary>
        public void ClearMouseEventHandler()
        {
            MouseButtonChanged = null;
            MouseWheelChanged = null;
        }

        /// <summary>
        /// 状態コンテキスト
        /// </summary>
        private MouseInputContext _context;


        /// <summary>
        /// コンストラクター
        /// </summary>
        private MouseInput()
        {
            _context = new MouseInputContext(MainWindow.Current.MainView, MouseGestureCommandCollection.Current);
            _sender = _context.Sender;

            this.Normal = new MouseInputNormal(_context);
            this.Normal.StateChanged += StateChanged;
            this.Normal.MouseButtonChanged += (s, e) => MouseButtonChanged?.Invoke(_sender, e);
            this.Normal.MouseWheelChanged += (s, e) => MouseWheelChanged?.Invoke(_sender, e);

            this.Loupe = new MouseInputLoupe(_context);
            this.Loupe.StateChanged += StateChanged;
            this.Loupe.MouseButtonChanged += (s, e) => MouseButtonChanged?.Invoke(_sender, e);
            this.Loupe.MouseWheelChanged += (s, e) => MouseWheelChanged?.Invoke(_sender, e);

            this.Drag = new MouseInputDrag(_context);
            this.Drag.StateChanged += StateChanged;
            this.Drag.MouseButtonChanged += (s, e) => MouseButtonChanged?.Invoke(_sender, e);
            this.Drag.MouseWheelChanged += (s, e) => MouseWheelChanged?.Invoke(_sender, e);

            this.Gesture = new MouseInputGesture(_context);
            this.Gesture.StateChanged += StateChanged;
            this.Gesture.MouseButtonChanged += (s, e) => MouseButtonChanged?.Invoke(_sender, e);
            this.Gesture.MouseWheelChanged += (s, e) => MouseWheelChanged?.Invoke(_sender, e);
            this.Gesture.GestureChanged += (s, e) => _context.GestureCommandCollection.Execute(e.Sequence);
            this.Gesture.GestureProgressed += (s, e) => _context.GestureCommandCollection.ShowProgressed(e.Sequence);

            // initialize state
            _mouseInputCollection = new Dictionary<MouseInputState, MouseInputBase>();
            _mouseInputCollection.Add(MouseInputState.Normal, this.Normal);
            _mouseInputCollection.Add(MouseInputState.Loupe, this.Loupe);
            _mouseInputCollection.Add(MouseInputState.Drag, this.Drag);
            _mouseInputCollection.Add(MouseInputState.Gesture, this.Gesture);
            SetState(MouseInputState.Normal, null);

            // initialize event
            // NOTE: 時々操作が奪われしてまう原因の可能性その１
            _sender.MouseDown += OnMouseButtonDown;
            _sender.MouseUp += OnMouseButtonUp;
            _sender.MouseWheel += OnMouseWheel;
            _sender.MouseMove += OnMouseMove;
            _sender.PreviewKeyDown += OnKeyDown;
        }



        //
        public bool IsCaptured()
        {
            return _current.IsCaptured();
        }


        /// <summary>
        /// 状態変更イベント処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StateChanged(object sender, MouseInputStateEventArgs e)
        {
            SetState(e.State, e.Parameter);
        }

        /// <summary>
        /// 状態変更
        /// </summary>
        /// <param name="state"></param>
        /// <param name="parameter"></param>
        public void SetState(MouseInputState state, object parameter, bool force = false)
        {
            if (!force && state == _state) return;
            ////Debug.WriteLine($"#MouseState: {state}");

            var inputOld = _current;
            var inputNew = _mouseInputCollection[state];

            inputOld?.OnClosed(_sender);
            _state = state;
            _current = inputNew;
            inputNew?.OnOpened(_sender, parameter);

            // NOTE: MouseCaptureの影響で同じUIスレッドで再入する可能性があるため、まとめて処理
            inputOld?.OnCaptureClosed(_sender);
            inputNew?.OnCaptureOpened(_sender);
        }


        /// <summary>
        /// 状態初期化
        /// </summary>
        public void ResetState()
        {
            SetState(MouseInputState.Normal, null, true);
        }



        /// <summary>
        /// IsLoupeMode property.
        /// </summary>
        public bool IsLoupeMode
        {
            get { return _state == MouseInputState.Loupe; }
            set { SetState(value ? MouseInputState.Loupe : MouseInputState.Normal, false); }
        }

        //
        public bool IsNormalMode => _state == MouseInputState.Normal;

        //
        private bool IsStylusDevice(MouseEventArgs e) => e.StylusDevice != null && Config.Current.Touch.IsEnabled;

        /// <summary>
        /// OnMouseButtonDown
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender != _sender) return;
            if (IsStylusDevice(e)) return;
            if (MainWindow.Current.IsMouseActivate) return;

            _current.OnMouseButtonDown(_sender, e);
        }

        /// <summary>
        /// OnMouseButtonUp
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMouseButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender != _sender) return;

            if (!IsStylusDevice(e))
            {
                _current.OnMouseButtonUp(_sender, e);
            }

            // 右クリックでのコンテキストメニュー無効
            e.Handled = true;
        }

        /// <summary>
        /// OnMouseWheel
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender != _sender) return;
            if (IsStylusDevice(e)) return;

            _current.OnMouseWheel(_sender, e);
        }

        // マウス移動検知用
        private Point _lastActionPoint;

        /// <summary>
        /// OnMouseMove
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (sender != _sender) return;
            if (IsStylusDevice(e)) return;

            _current.OnMouseMove(_sender, e);

            // マウス移動を通知
            var nowPoint = e.GetPosition(_sender);
            if (Math.Abs(nowPoint.X - _lastActionPoint.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(nowPoint.Y - _lastActionPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                MouseMoved?.Invoke(this, null);
                _lastActionPoint = nowPoint;
            }
        }

        /// <summary>
        /// OnKeyDown
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (sender != _sender) return;
            _current.OnKeyDown(_sender, e);
        }

        /// <summary>
        /// Cancel input
        /// </summary>
        public void Cancel()
        {
            _current.Cancel();
        }


        // メッセージとして状態表示
        public void ShowMessage(TransformActionType ActionType, ViewContent mainContent)
        {
            var infoMessage = InfoMessage.Current;
            if (Config.Current.Notice.ViewTransformShowMessageStyle == ShowMessageStyle.None) return;

            var transform = DragTransform.Current;

            switch (ActionType)
            {
                case TransformActionType.Scale:
                    string scaleText = Config.Current.Notice.IsOriginalScaleShowMessage && mainContent != null && mainContent.IsValid
                        ? $"{(int)(transform.Scale * mainContent.Scale * Environment.Dpi.DpiScaleX * 100 + 0.1)}%"
                        : $"{(int)(transform.Scale * 100.0 + 0.1)}%";
                    infoMessage.SetMessage(InfoMessageType.ViewTransform, scaleText);
                    break;
                case TransformActionType.Angle:
                    infoMessage.SetMessage(InfoMessageType.ViewTransform, $"{(int)(transform.Angle)}°");
                    break;
                case TransformActionType.FlipHorizontal:
                    infoMessage.SetMessage(InfoMessageType.ViewTransform, Properties.Resources.NotifyFlipHorizontal + " " + (transform.IsFlipHorizontal ? "ON" : "OFF"));
                    break;
                case TransformActionType.FlipVertical:
                    infoMessage.SetMessage(InfoMessageType.ViewTransform, Properties.Resources.NotifyFlipVertical + " " + (transform.IsFlipVertical ? "ON" : "OFF"));
                    break;
                case TransformActionType.LoupeScale:
                    if (LoupeTransform.Current.Scale != 1.0)
                    {
                        infoMessage.SetMessage(InfoMessageType.ViewTransform, $"×{LoupeTransform.Current.Scale:0.0}");
                    }
                    break;
            }
        }

        #region Memento
        [DataContract]
        public class Memento : IMemento
        {
            [DataMember]
            public MouseInputNormal.Memento Normal { get; set; }
            [DataMember]
            public MouseInputLoupe.Memento Loupe { get; set; }
            [DataMember]
            public MouseInputGesture.Memento Gesture { get; set; }

            #region Obsolete
            [Obsolete, DataMember(EmitDefaultValue = false)] // ver 34.0
            public MouseInputDrag.Memento Drag { get; set; }
            #endregion


            public void RestoreConfig(Config config)
            {
                Normal.RestoreConfig(config);
                Loupe.RestoreConfig(config);
                Gesture.RestoreConfig(config);
            }
        }

        #endregion

    }

}
