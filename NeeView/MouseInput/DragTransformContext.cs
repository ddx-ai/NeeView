﻿using System;
using System.Windows;
using System.Windows.Input;
using NeeView.ComponentModel;
using NeeView.PageFrames;

namespace NeeView
{
    /// <summary>
    /// 表示座標系操作のリソース
    /// </summary>
    public class DragTransformContext 
    {
        private readonly Speedometer _speedometer = new();
        private readonly ICanvasToViewTranslator _canvasToViewTranslator;


        public DragTransformContext(FrameworkElement sender, ITransformControl transform, PageFrameContainer container, ICanvasToViewTranslator canvasToViewTranslator, ViewConfig viewConfig, MouseConfig mouseConfig)
        {
            Sender = sender;
            Container = container;
            _canvasToViewTranslator = canvasToViewTranslator;
            ViewRect = CreateViewRect();
            ContentRect = CreateContentRect(Container);
            ViewConfig = viewConfig;
            MouseConfig = mouseConfig;
            Transform = transform;
        }

        public ViewConfig ViewConfig { get; }

        public MouseConfig MouseConfig { get; }

        public ITransformControl Transform { get; }

        public FrameworkElement Sender { get; }

        // NOTE: 画面中央を(0,0)とした座標系
        public Point OriginPoint { get; set; }
        public Point First { get; set; }
        public Point Old { get; set; }
        public Point Last { get; set; }

        public int FirstTimeStamp { get; set; }
        public int OldTimeStamp { get; set; }
        public int LastTimeStamp { get; set; }
        
        public PageFrameContainer Container { get; }

        public Rect ViewRect { get; private set; }
        public Rect ContentRect { get; private set; }
        public Point ContentCenter => ContentRect.Center();

        public Point BasePoint { get; set; }
        public double BaseAngle { get; set; }
        public double BaseScale { get; set; }
        public bool BaseFlipHorizontal { get; set; }
        public bool BaseFlipVertical { get; set; }

        public Point RotateCenter { get; set; }
        public Point ScaleCenter { get; set; }
        public Point FlipCenter { get; set; }

        public Speedometer Speedometer => _speedometer;

        public TimeSpan ScrollDuration => TimeSpan.FromSeconds(Config.Current.View.ScrollDuration);


        public virtual void Initialize(Point point, int timestamp)
        {
            OriginPoint = point;

            First = point;
            Old = point;
            Last = point;

            FirstTimeStamp = timestamp;
            OldTimeStamp = timestamp;
            LastTimeStamp = timestamp;

            _speedometer.Reset(point, timestamp);

            BasePoint = Transform.Point;
            BaseAngle = Transform.Angle;
            BaseScale = Transform.Scale;
            BaseFlipHorizontal = Transform.IsFlipHorizontal;
            BaseFlipVertical = Transform.IsFlipVertical;

            RotateCenter = GetCenterPosition(ViewConfig.RotateCenter);
            ScaleCenter = GetCenterPosition(ViewConfig.ScaleCenter);
            FlipCenter = GetCenterPosition(ViewConfig.FlipCenter);
        }


        private Point GetCenterPosition(DragControlCenter dragControlCenter)
        {
            return dragControlCenter switch
            {
                DragControlCenter.View => ViewRect.Center(), // NOTE: 常に(0,0)
                DragControlCenter.Target => ContentRect.Center(),
                DragControlCenter.Cursor => First,
                _ => throw new NotImplementedException(),
            };
        }


        public void Update(Point point, int timestamp, DragActionUpdateOptions options)
        {
            if (!options.HasFlag(DragActionUpdateOptions.IgnoreUpdateState))
            {
                Old = Last;
                OldTimeStamp = LastTimeStamp;
                Last = point;
                LastTimeStamp = timestamp;
            }
            if (!options.HasFlag(DragActionUpdateOptions.IgnoreUpdateSpeed))
            {
                _speedometer.Update(point, timestamp);
            }
        }

        public void UpdateRect()
        {
            ViewRect = CreateViewRect();
            ContentRect = CreateContentRect(Container);
        }

        private Rect CreateViewRect()
        {
            var viewRect = new Size(Sender.ActualWidth, Sender.ActualHeight).ToRect();
            return viewRect;
        }

        private Rect CreateContentRect(PageFrameContainer container)
        {
            var rect = container.GetContentRect();
            var p0 = _canvasToViewTranslator.TranslateCanvasToViewPoint(container.TranslateContentToCanvasPoint(rect.TopLeft));
            var p1 = _canvasToViewTranslator.TranslateCanvasToViewPoint(container.TranslateContentToCanvasPoint(rect.BottomRight));
            var contentRect = new Rect(p0, p1);
            return contentRect;
        }
    }


    [Flags]
    public enum DragActionUpdateOptions
    {
        None = 0,
        IgnoreUpdateState = (1 << 0),
        IgnoreUpdateSpeed = (1 << 1),
    }
}