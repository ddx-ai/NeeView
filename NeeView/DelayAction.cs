﻿// Copyright (c) 2016 Mitsuhiro Ito (nee)
//
// This software is released under the MIT License.
// http://opensource.org/licenses/mit-license.php

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace NeeView
{
    /// <summary>
    /// 遅延実行
    /// コマンドを遅延実行する。遅延中に要求された場合は古いコマンドをキャンセルする。
    /// </summary>
    public class DelayAction
    {
        /// <summary>
        /// 遅延実行要求
        /// </summary>
        private bool _isRequested;

        /// <summary>
        /// 最後のGC要求時間
        /// </summary>
        private DateTime _lastRequestTime;

        /// <summary>
        /// 遅延実行のためのタイマー
        /// </summary>
        private DispatcherTimer _timer;

        /// <summary>
        /// 遅延時間
        /// </summary>
        private TimeSpan _delay;

        /// <summary>
        /// 実行本体
        /// </summary>
        private Action _action;
        

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="dispatcher"></param>
        public DelayAction(Dispatcher dispatcher, Action action, TimeSpan delay)
        {
            // timer for delay
            _timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher);
            _timer.Interval = TimeSpan.FromSeconds(0.1);
            _timer.Tick += new EventHandler(DispatcherTimer_Tick);

            _action = action;
            _delay = delay;
        }

        /// <summary>
        /// 実行要求
        /// </summary>
        public void Request()
        {
            _lastRequestTime = DateTime.Now;
            _isRequested = true;
            _timer.Start();
        }

        /// <summary>
        /// 実行キャンセル
        /// </summary>
        public void Cancel()
        {
            _timer.Stop();
            _isRequested = false;
        }


        /// <summary>
        /// timer callback
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            if ((DateTime.Now - _lastRequestTime) > _delay)
            {
                _timer.Stop();
                if (_isRequested)
                {
                    _isRequested = false;
                    _action?.Invoke();
                }
            }
        }

    }

}
