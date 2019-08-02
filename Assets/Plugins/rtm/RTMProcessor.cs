using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GameDevWare.Serialization;
using com.fpnn;

// using UnityEngine;

namespace com.rtm {

    public class RTMProcessor:FPProcessor.IProcessor {

        private class PingLocker {

            public int Status = 0;
        }

        private Hashtable _midMap = new Hashtable();

        private object action_locker = new object();
        private IDictionary<string, Action<IDictionary<string, object>>> _actionDict = new Dictionary<string, Action<IDictionary<string, object>>>();

        private Type _type;

        public RTMProcessor(FPEvent evt) {

            this._type = Type.GetType("com.rtm.RTMProcessor");
        }

        public void Destroy() {

            this.ClearPingTimestamp();
            
            this._midMap.Clear();
            this._actionDict.Clear();
        }

        public void Service(FPData data, AnswerDelegate answer) {

            bool callCb = true;

            if (RTMConfig.KICKOUT == data.GetMethod()) {

                callCb = false;
            }

            if (RTMConfig.SERVER_PUSH.kickOutRoom == data.GetMethod()) {

                callCb = false;
            }

            IDictionary<string, object> payload = null;

            if (data.GetFlag() == 0) {

                if (callCb) {

                    answer(Json.SerializeToString(new Dictionary<string, object>()), false);
                }

                try {

                    payload = Json.Deserialize<IDictionary<string, object>>(data.JsonPayload());
                }catch(Exception ex) {

                    ErrorRecorderHolder.recordError(ex);
                }
            }

            if (data.GetFlag() == 1) {

                if (callCb) {

                    using (MemoryStream msgpackStream = new MemoryStream()) {

                        MsgPack.Serialize(new Dictionary<string, object>(), msgpackStream);
                        msgpackStream.Seek(0, SeekOrigin.Begin);

                        answer(msgpackStream.ToArray(), false);
                    }
                }

                try {

                    using (MemoryStream inputStream = new MemoryStream(data.MsgpackPayload())) {

                        payload = MsgPack.Deserialize<IDictionary<string, object>>(inputStream);
                    }
                } catch(Exception ex) {

                   ErrorRecorderHolder.recordError(ex);
                }
            }

            if (payload != null) {

                MethodInfo method = this._type.GetMethod(data.GetMethod());

                if (method != null) {

                    object[] paras = new object[] {payload};
                    method.Invoke(this, paras);
                }
            }
        }

        public bool HasPushService(string name) {

            if (string.IsNullOrEmpty(name)) {

                return false;
            }

            lock (action_locker) {

                return this._actionDict.ContainsKey(name);
            }
        }

        public void AddPushService(string name, Action<IDictionary<string, object>> action) {

            lock (action_locker) {

                if (!this._actionDict.ContainsKey(name)) {

                    this._actionDict.Add(name, action);
                } else {

                    ErrorRecorderHolder.recordError(new Exception("push service exist"));
                }
            }
        }

        public void RemovePushService(string name) {

            lock (action_locker) {

                if (this._actionDict.ContainsKey(name)) {

                    this._actionDict.Remove(name);
                }
            }
        }

        private void PushService(string name, IDictionary<string, object> data) {

            lock (action_locker) {

                if (this._actionDict.ContainsKey(name)) {

                    Action<IDictionary<string, object>> action = this._actionDict[name];

                    if (action != null) {

                        action(data);
                    }
                }
            }
        }

        /**
         * @param {IDictionary<string, object>} data
         */
        public void kickout(IDictionary<string, object> data) {

            this.PushService(RTMConfig.KICKOUT, data);
        }

        /**
         * @param {long} data.rid
         */
        public void kickoutroom(IDictionary<string, object> data) {

            this.PushService(RTMConfig.SERVER_PUSH.kickOutRoom, data);
        }

        /**
         * @param {long}   data.from
         * @param {long}   data.to
         * @param {byte}   data.mtype
         * @param {long}   data.mid
         * @param {string} data.msg
         * @param {string} data.attrs
         * @param {long}   data.mtime
         */
        public void pushmsg(IDictionary<string, object> data) {

            if (data.ContainsKey("mid")) {

                if (!this.CheckMid(1, (long)Convert.ToInt64(data["mid"]), (long)Convert.ToInt64(data["from"]), 0)) {

                    return;
                }
            }

            byte mtype = Convert.ToByte(data["mtype"]);
            string name = RTMConfig.SERVER_PUSH.recvMessage;

            if (mtype >= 40 && mtype <= 50) {

                name = RTMConfig.SERVER_PUSH.recvFile;
            }

            this.PushService(name, data);
        }

        /**
         * @param {long}   data.from
         * @param {long}   data.gid
         * @param {byte}   data.mtype
         * @param {long}   data.mid
         * @param {string} data.msg
         * @param {string} data.attrs
         * @param {long}   data.mtime
         */
        public void pushgroupmsg(IDictionary<string, object> data) {

            if (data.ContainsKey("mid")) {

                if (!this.CheckMid(2, (long)Convert.ToInt64(data["mid"]), (long)Convert.ToInt64(data["from"]), (long)Convert.ToInt64(data["gid"]))) {

                    return;
                }
            }

            byte mtype = Convert.ToByte(data["mtype"]);
            string name = RTMConfig.SERVER_PUSH.recvGroupMessage;

            if (mtype >= 40 && mtype <= 50) {

                name = RTMConfig.SERVER_PUSH.recvGroupFile;
            }

            this.PushService(name, data);
        }

        /**
         * @param {long}   data.from
         * @param {long}   data.rid
         * @param {byte}   data.mtype
         * @param {long}   data.mid
         * @param {string} data.msg
         * @param {string} data.attrs
         * @param {long}   data.mtime
         */
        public void pushroommsg(IDictionary<string, object> data) {

            if (data.ContainsKey("mid")) {

                if (!this.CheckMid(3, (long)Convert.ToInt64(data["mid"]), (long)Convert.ToInt64(data["from"]), (long)Convert.ToInt64(data["rid"]))) {

                    return;
                }
            }

            byte mtype = Convert.ToByte(data["mtype"]);
            string name = RTMConfig.SERVER_PUSH.recvRoomMessage;

            if (mtype >= 40 && mtype <= 50) {

                name = RTMConfig.SERVER_PUSH.recvRoomFile;
            }

            this.PushService(name, data);
        }

        /**
         * @param {long}   data.from
         * @param {byte}   data.mtype
         * @param {long}   data.mid
         * @param {string} data.msg
         * @param {string} data.attrs
         * @param {long}   data.mtime
         */
        public void pushbroadcastmsg(IDictionary<string, object> data) {

            if (data.ContainsKey("mid")) {

                if (!this.CheckMid(4, (long)Convert.ToInt64(data["mid"]), (long)Convert.ToInt64(data["from"]), 0)) {

                    return;
                }
            }

            byte mtype = Convert.ToByte(data["mtype"]);
            string name = RTMConfig.SERVER_PUSH.recvBroadcastMessage;

            if (mtype >= 40 && mtype <= 50) {

                name = RTMConfig.SERVER_PUSH.recvBroadcastFile;
            }

            this.PushService(name, data);
        }

        /**
         * @param {Map} data
         */
        public void ping(IDictionary<string, object> data) {

            this.PushService(RTMConfig.SERVER_PUSH.recvPing, data);

            lock (ping_locker) {
                
                this._lastPingTimestamp = FPManager.Instance.GetMilliTimestamp();
            }
        }

        private long _lastPingTimestamp;
        private PingLocker ping_locker = new PingLocker();

        public long GetPingTimestamp() {

            lock (ping_locker) {

                return this._lastPingTimestamp;
            }
        }

        public void ClearPingTimestamp() {

            lock (ping_locker) {

                this._lastPingTimestamp = 0;
            }
        }

        public void InitPingTimestamp() {

            lock (ping_locker) {

                if (this._lastPingTimestamp == 0) {

                    this._lastPingTimestamp = FPManager.Instance.GetMilliTimestamp();
                }
            }
        }

        public void OnSecond(long timestamp) {

            this.CheckExpire(timestamp);
        }

        private bool CheckMid(int type, long mid, long uid, long rgid) {

            StringBuilder sb = new StringBuilder(50);

            sb.Append(Convert.ToString(type));
            sb.Append("_");
            sb.Append(Convert.ToString(mid));
            sb.Append("_");
            sb.Append(Convert.ToString(uid));

            if (rgid > 0) {

                sb.Append("_");
                sb.Append(Convert.ToString(rgid));
            }

            string key = sb.ToString();

            lock (this._midMap) {

                long timestamp = FPManager.Instance.GetMilliTimestamp();

                if (this._midMap.ContainsKey(key)) {

                    long expire = (long)Convert.ToInt64(this._midMap[key]);

                    if (expire > timestamp) {

                        return false;
                    }

                    this._midMap.Remove(key);
                }

                this._midMap.Add(key, RTMConfig.MID_TTL + timestamp);
                return true;
            }
        }

        private void CheckExpire(long timestamp) {

            lock (this._midMap) {

                List<string> keys = new List<string>();

                foreach (DictionaryEntry entry in this._midMap) {

                    string key = (string)entry.Key;
                    long expire = (long)entry.Value;

                    if (expire > timestamp) {

                        continue;
                    }

                    keys.Add(key);
                }

                foreach (string rkey in keys) {

                    this._midMap.Remove(rkey);
                }
            }
        }
    }
}