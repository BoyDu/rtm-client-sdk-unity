using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

namespace com.fpnn {

    public delegate void AnswerDelegate(object payload, bool exception);

    public class FPProcessor {

        public interface IProcessor {

            void Service(FPData data, AnswerDelegate answer);
            void OnSecond(long timestamp);
            bool HasPushService(string name);
        }

        private class ServiceLocker {

            public int Status = 0;
        }

        private class BaseProcessor:IProcessor {

            private FPEvent _event = new FPEvent();

            public void Service(FPData data, AnswerDelegate answer) {

                // TODO 
                if (data.GetFlag() == 0) {}
                if (data.GetFlag() == 1) {}
            }

            public bool HasPushService(string name) {

                return false;
            }

            public void OnSecond(long timestamp) {}
        }

        private bool _destroyed;
        private IProcessor _processor;
        private object self_locker = new object();

        public void SetProcessor(IProcessor processor) {

            lock (self_locker) {

                this._processor = processor;
            }
        }

        private Thread _serviceThread = null;
        private ManualResetEvent _serviceEvent = new ManualResetEvent(false);

        private ServiceLocker service_locker = new ServiceLocker();

        private void StartServiceThread() {

            lock (self_locker) {

                if (this._destroyed) {

                    return;
                }
            }

            lock (service_locker) {

                if (service_locker.Status != 0) {

                    return;
                }

                service_locker.Status = 1;
                this._serviceEvent.Reset();

                this._serviceThread = new Thread(new ThreadStart(ServiceThread));

                if (this._serviceThread.Name == null) {

                    this._serviceThread.Name = "fpnn_push_thread";
                }

                this._serviceThread.Start();
            }
        }

        private void ServiceThread() {

            try {

                while (true) {

                    this._serviceEvent.WaitOne();

                    List<ServiceDelegate> list;

                    lock (service_locker) {

                        if (service_locker.Status == 0) {

                            return;
                        }

                        list = this._serviceCache;
                        this._serviceCache = new List<ServiceDelegate>();
                    }

                    this._serviceEvent.Reset();
                    this.CallService(list);
                }
            } catch (ThreadAbortException tex) {
            } catch (Exception ex) {

                ErrorRecorderHolder.recordError(ex);
            } finally {

                this.StopServiceThread(false);
            }
        }

        private void CallService(ICollection<ServiceDelegate> list) {

            foreach (ServiceDelegate service in list) {

                if (service != null) {

                    try {

                        service();
                    } catch(Exception ex) {

                        ErrorRecorderHolder.recordError(ex);
                    }
                }
            }
        }

        private void StopServiceThread(bool destroy) {

            lock (service_locker) {

                if (service_locker.Status != 0) {

                    service_locker.Status = 0;
                    this._serviceEvent.Set();
                }
            }

            if (destroy) {

                this._serviceEvent.Close(); 
            }
        }

        private List<ServiceDelegate> _serviceCache = new List<ServiceDelegate>();

        public void Service(FPData data, AnswerDelegate answer) {

            lock (self_locker) {

                if (this._processor == null) {

                    this._processor = new BaseProcessor();
                }

                if (!this._processor.HasPushService(data.GetMethod())) {

                    if (data.GetMethod() != "ping") {

                        return;
                    }
                }
            }

            FPProcessor self = this;

            this.AddService(() => {

                lock (self_locker) {

                    self._processor.Service(data, answer);
                }
            });
        }

        private void AddService(ServiceDelegate service) {

            this.StartServiceThread();

            lock (service_locker) {

                if (this._serviceCache.Count < 3000) {

                    this._serviceCache.Add(service);
                } 

                if (this._serviceCache.Count == 2998) {

                    ErrorRecorderHolder.recordError(new Exception("Push Calls Limit!"));
                }
            } 

            this._serviceEvent.Set();
        }

        public void OnSecond(long timestamp) {

            lock (self_locker) {

                if (this._processor != null) {

                    this._processor.OnSecond(timestamp);
                }
            }
        }

        public void Destroy() {

            lock (self_locker) {

                if (this._destroyed) {

                    return;
                }

                this._destroyed = true;
            }

            this.StopServiceThread(true);
        }
    }
}