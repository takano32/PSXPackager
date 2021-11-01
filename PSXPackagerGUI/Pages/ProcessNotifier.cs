﻿using System.Windows.Threading;
using PSXPackager.Common;
using PSXPackager.Common.Notification;

namespace PSXPackagerGUI.Pages
{
    public class ProcessNotifier : INotifier
    {
        private readonly Dispatcher _dispatcher;
        private double _lastvalue;
        private string _action;
        public ProcessNotifier(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public BatchEntryModel Entry { get; set; }

        public void Notify(PopstationEventEnum @event, object value)
        {
            switch (@event)
            {
                case PopstationEventEnum.ProcessingStart:
                    break;

                case PopstationEventEnum.ProcessingComplete:
                    Entry.MaxProgress = 100;
                    Entry.Progress = 0;
                    break;

                case PopstationEventEnum.Error:
                    Entry.Status = "Error";
                    Entry.MaxProgress = 100;
                    Entry.Progress = 100;
                    Entry.HasError = true;
                    Entry.ErrorMessage += (string) value + "\r\n";

                    break;

                case PopstationEventEnum.FileName:
                case PopstationEventEnum.Info:
                    break;

                case PopstationEventEnum.Warning:
                    break;

                case PopstationEventEnum.GetIsoSize:
                    _lastvalue = 0;
                    Entry.MaxProgress = (uint)value;
                    Entry.Progress = 0;
                    break;

                case PopstationEventEnum.ConvertSize:
                case PopstationEventEnum.ExtractSize:
                case PopstationEventEnum.WriteSize:
                    _lastvalue = 0;
                    Entry.MaxProgress = (uint)value;
                    Entry.Progress = 0;
                    break;

                case PopstationEventEnum.ConvertStart:
                    _action = "Converting";
                    break;

                case PopstationEventEnum.WriteStart:
                    _action = $"Writing Disc {value}";

                    break;

                case PopstationEventEnum.ExtractStart:
                    _action = "Extracting";
                    break;

                case PopstationEventEnum.DecompressStart:
                    _action = "Decompressing";

                    break;

                case PopstationEventEnum.ExtractComplete:
                case PopstationEventEnum.WriteComplete:
                case PopstationEventEnum.DecompressComplete:
                    Entry.Status = "Complete";
                    //Entry.MaxProgress = 100;
                    //Entry.Progress = 0;
                    break;

                case PopstationEventEnum.ConvertComplete:

                    break;


                case PopstationEventEnum.ConvertProgress:
                case PopstationEventEnum.ExtractProgress:
                case PopstationEventEnum.WriteProgress:
                    _dispatcher.Invoke(() =>
                    {
                        var percent = (uint)value / (float)Entry.MaxProgress * 100f;
                        if (percent - _lastvalue >= 0.25)
                        {
                            Entry.Status = $"{_action} ({percent:F0}%)";
                            Entry.Progress = (uint)value;
                            _lastvalue = percent;
                        }
                    });

                    break;

                case PopstationEventEnum.DecompressProgress:
                    break;
            }
        }
    }
}