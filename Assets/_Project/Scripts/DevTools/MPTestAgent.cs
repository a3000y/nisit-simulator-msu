#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.DevTools
{
    // ===== ตัวช่วยทดสอบ Multiplayer หลาย instance (Editor / Development Build เท่านั้น — Release ไม่มีโค้ดนี้) =====
    //   เปิดใช้: Development Build ด้วย args  -mptest <โฟลเดอร์ทดสอบ> -mptest-id <ชื่อ instance>
    //            Editor: เรียก MPTestAgent.Enable("H", dir) ระหว่าง Play Mode
    //   • แยกโปรไฟล์: เซฟทั้งหมดไปที่ <dir>/<id>_save.json + SaveSystem.DevGuard (ห้ามเขียน/ลบเซฟช่องจริง)
    //   • รับคำสั่งจากไฟล์ <dir>/<id>.cmd (บรรทัดละคำสั่ง) → ทำแล้วลบไฟล์ · ผล/เหตุการณ์ → <dir>/<id>.log (มีเวลา + ClientId)
    //   • เขียนสถานะ <dir>/<id>.state.json ทุก 0.5 วินาที (บทบาท, ClientId, avatar, สถานะผู้เล่น, เวลาโลก ...)
    //   • ไม่มีการตรวจสิทธิ์ใดถูกปิด — คำสั่งทุกตัวเรียกโค้ดเกมเส้นทางเดิม (ปุ่ม UI / API สาธารณะ)
    public class MPTestAgent : MonoBehaviour
    {
        public static MPTestAgent Instance { get; private set; }
        public string Id { get; private set; } = "X";
        public string Dir { get; private set; } = "";

        string CmdPath => Path.Combine(Dir, Id + ".cmd");
        string LogPath => Path.Combine(Dir, Id + ".log");
        string StatePath => Path.Combine(Dir, Id + ".state.json");
        public string SavePath => Path.Combine(Dir, Id + "_save.json");

        float nextPoll, nextState;
        NetworkManager hooked;
        readonly List<string> recentErrors = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            string dir = null, id = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-mptest") dir = args[i + 1];
                if (args[i] == "-mptest-id") id = args[i + 1];
            }
            if (!string.IsNullOrEmpty(dir)) Enable(string.IsNullOrEmpty(id) ? "C" : id, dir);
        }

        public static MPTestAgent Enable(string id, string dir)
        {
            if (Instance == null)
            {
                var go = new GameObject("MPTestAgent");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<MPTestAgent>();
            }
            Instance.Setup(id, dir);
            return Instance;
        }

        void Setup(string id, string dir)
        {
            Id = id; Dir = dir;
            Directory.CreateDirectory(Dir);
            // โปรไฟล์ทดสอบแยกต่อ instance — ห้ามแตะเซฟช่องจริง
            SaveSystem.DevGuard = true;
            SaveSystem.DevPathOverride = SavePath;
            if (string.IsNullOrEmpty(GameSession.PlayerName)) GameSession.PlayerName = "Test-" + id;
            Application.runInBackground = true;
            Application.logMessageReceived -= OnUnityLog;
            Application.logMessageReceived += OnUnityLog;
            Log($"agent ready · pid={System.Diagnostics.Process.GetCurrentProcess().Id} · save={SavePath} · name={GameSession.PlayerName}");
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnUnityLog;
            if (Instance == this) Instance = null;
        }

        // ---------- log ----------
        public void Log(string msg)
        {
            var nm = NetworkManager.Singleton;
            string cid = nm != null && nm.IsListening ? nm.LocalClientId.ToString() : "-";
            string role = nm == null || !nm.IsListening ? "off" : nm.IsHost ? "host" : nm.IsServer ? "server" : "client";
            string line = $"{DateTime.Now:HH:mm:ss.fff} [{Id}] [{role} cid={cid}] {msg}";
            try { File.AppendAllText(LogPath, line + "\n", Encoding.UTF8); } catch { }
        }

        void OnUnityLog(string condition, string stack, LogType type)
        {
            bool important = type == LogType.Error || type == LogType.Exception || type == LogType.Assert || type == LogType.Warning
                             || condition.StartsWith("[Net") || condition.StartsWith("[Spawn") || condition.StartsWith("[Save")
                             || condition.StartsWith("[Relay") || condition.StartsWith("[Netcode") || condition.StartsWith("[MP");
            if (!important) return;
            string s = $"UNITY {type}: {condition}";
            if (type == LogType.Exception || type == LogType.Error)
            {
                var lines = (stack ?? "").Split('\n');
                var mine = lines.FirstOrDefault(l => l.Contains("NisitSimulator"));
                s += " | " + (mine ?? lines.FirstOrDefault());
                recentErrors.Add(condition.Length > 200 ? condition.Substring(0, 200) : condition);
                if (recentErrors.Count > 20) recentErrors.RemoveAt(0);
            }
            Log(s);
        }

        // ---------- NetworkManager hooks ----------
        void HookNetwork()
        {
            var nm = NetworkManager.Singleton;
            if (nm == hooked) return;
            if (hooked != null)
            {
                hooked.OnConnectionEvent -= OnConn;
                hooked.OnTransportFailure -= OnTransportFailure;
                hooked.OnClientStopped -= OnStopped;
            }
            hooked = nm;
            if (nm == null) return;
            nm.OnConnectionEvent += OnConn;
            nm.OnTransportFailure += OnTransportFailure;
            nm.OnClientStopped += OnStopped;
            Log("hooked NetworkManager " + nm.gameObject.scene.name);
        }

        void OnConn(NetworkManager nm, ConnectionEventData e)
        {
            string reason = nm != null ? nm.DisconnectReason : "";
            Log($"NET {e.EventType} client={e.ClientId}" + (string.IsNullOrEmpty(reason) ? "" : $" reason='{reason}'"));
        }
        void OnTransportFailure() => Log("NET transport failure");
        void OnStopped(bool wasHost) => Log($"NET client stopped (wasHost={wasHost}) reason='{NetworkManager.Singleton?.DisconnectReason}'");

        // ---------- loop ----------
        // DevProfile รีเซ็ต DevGuard ตอนเปลี่ยนฉาก → บังคับโปรไฟล์ทดสอบซ้ำทุกเฟรม (ห้ามเขียน/ลบเซฟช่องจริงเด็ดขาด)
        void EnforceProfile()
        {
            if (string.IsNullOrEmpty(Dir)) return;
            SaveSystem.DevGuard = true;
            SaveSystem.DevPathOverride = SavePath;
        }

        void LateUpdate() => EnforceProfile();

        void Update()
        {
            EnforceProfile();
            HookNetwork();
            if (Time.unscaledTime >= nextPoll)
            {
                nextPoll = Time.unscaledTime + 0.2f;
                PollCommands();
            }
            if (Time.unscaledTime >= nextState)
            {
                nextState = Time.unscaledTime + 0.5f;
                WriteState();
            }
        }

        void PollCommands()
        {
            if (!File.Exists(CmdPath)) return;
            string[] lines;
            try { lines = File.ReadAllLines(CmdPath, Encoding.UTF8); File.Delete(CmdPath); }
            catch { return; }   // ไฟล์กำลังถูกเขียน → รอบหน้า
            StartCoroutine(RunLines(lines));
        }

        IEnumerator RunLines(string[] lines)
        {
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("wait "))
                {
                    float s = F(line.Substring(5));
                    Log("> " + line);
                    yield return new WaitForSecondsRealtime(s);
                    continue;
                }
                string result;
                try { result = Exec(line); }
                catch (Exception e) { result = "EXCEPTION " + (e.InnerException ?? e).GetType().Name + ": " + (e.InnerException ?? e).Message; }
                Log("> " + line + "  =>  " + result);
            }
            WriteState();
        }

        // ---------- commands ----------
        string Exec(string line)
        {
            var parts = Tokenize(line);
            string cmd = parts[0].ToLowerInvariant();
            var a = parts.Skip(1).ToArray();
            var nm = NetworkManager.Singleton;
            switch (cmd)
            {
                case "mark": return string.Join(" ", a);
                case "click": return Click(string.Join(" ", a));
                case "input": return SetInput(a[0], string.Join(" ", a.Skip(1)));
                case "scene": SceneManager.LoadScene(a[0]); return "loading " + a[0];
                case "shutdown": if (nm != null) nm.Shutdown(); return "shutdown";
                case "quit": Log("quit requested"); Application.Quit(); return "quit";
                case "hardexit": Log("hard exit (simulated crash)"); System.Diagnostics.Process.GetCurrentProcess().Kill(); return "killed";
                case "simnet": return SimNet(I(a[0]), I(a[1]), I(a[2]));
                case "tp": return Teleport(new Vector3(F(a[0]), F(a[1]), F(a[2])));
                case "walk": StartCoroutine(Walk(F(a[0]), F(a[1]), F(a[2]), a.Length > 3 && a[3] == "run")); return "walking";
                case "call": return Call(a[0], a.Skip(1).ToArray());
                case "get": return Describe(GetMember(a[0]));
                case "gettext": { var o = GetMember(a[0]); return o is TMPro.TMP_Text tt ? "\"" + tt.text + "\"" : o is TMPro.TMP_InputField fi ? "\"" + fi.text + "\"" : Describe(o); }
                case "settext": { var o = GetMember(a[0]); string v = string.Join(" ", a.Skip(1)); if (o is TMPro.TMP_InputField fi) { fi.text = v; return "set " + v; } if (o is TMPro.TMP_Text tt) { tt.text = v; return "set " + v; } return "not a text: " + Describe(o); }
                case "set": SetMember(a[0], a[1]); return "ok " + Describe(GetMember(a[0]));
                case "state": WriteState(); return "state written";
                case "rtt": return Rtt();
                case "hackavatar": return HackAvatar();
                case "hacktrade": return HackTrade(ulong.Parse(a[0]), int.Parse(a[1]));
                case "rpc": return RawNamedMessage(a);
                default: return "unknown command";
            }
        }

        // กดปุ่ม UI จริง (หาจากชื่อ GameObject หรือข้อความบนปุ่ม) — ใช้ onClick ของเกมเดิม
        string Click(string name)
        {
            foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Exclude))
            {
                var t = b.GetComponentInChildren<TMPro.TMP_Text>();
                if (b.name == name || (t != null && t.text.Trim() == name))
                {
                    if (!b.interactable) return "button not interactable: " + b.name;
                    b.onClick.Invoke();
                    return "clicked " + b.name;
                }
            }
            return "button not found/active: " + name;
        }

        string SetInput(string name, string value)
        {
            foreach (var f in FindObjectsByType<TMPro.TMP_InputField>(FindObjectsInactive.Include))
                if (f.name == name) { f.text = value; return "set " + name; }
            return "input not found: " + name;
        }

        // จำลองเครือข่ายหน่วง/ทำแพ็กเก็ตหาย ผ่าน DebugSimulator ของ UnityTransport (มีผลตอน Start ครั้งถัดไป — ต้องตั้งก่อน host/join)
        string SimNet(int delay, int jitter, int drop)
        {
            var nm = NetworkManager.Singleton;
            var utp = nm != null ? nm.GetComponent<UnityTransport>() : null;
            if (utp == null) return "no UnityTransport";
            var f = typeof(UnityTransport).GetField("DebugSimulator", BindingFlags.Public | BindingFlags.Instance);
            if (f == null) return "DebugSimulator not available in this transport version";
            object sim = f.GetValue(utp);
            var st = sim.GetType();
            st.GetField("PacketDelayMS").SetValue(sim, delay);
            st.GetField("PacketJitterMS").SetValue(sim, jitter);
            st.GetField("PacketDropRate").SetValue(sim, drop);
            f.SetValue(utp, sim);
            return $"DebugSimulator set delay={delay}ms jitter={jitter}ms drop={drop}% (applies on next start)";
        }

        // พยายามเขียนค่าตำแหน่ง/ชื่อของ avatar ผู้เล่นอื่น (ต้องถูก Netcode ปฏิเสธ)
        string HackAvatar()
        {
            foreach (var av in FindObjectsByType<NisitSimulator.Net.NetworkAvatar>())
            {
                if (av.IsOwner) continue;
                var f = typeof(NisitSimulator.Net.NetworkAvatar).GetField("netPos", BindingFlags.NonPublic | BindingFlags.Instance);
                var nv = (NetworkVariable<Vector3>)f.GetValue(av);
                var before = nv.Value;
                string r;
                try { nv.Value = before + new Vector3(50f, 0f, 0f); r = "write accepted?!"; }
                catch (Exception e) { r = "write rejected: " + e.GetType().Name + " " + e.Message; }
                return $"target owner={av.OwnerClientId} before={before} after={nv.Value} · {r}";
            }
            return "no remote avatar";
        }

        // ส่งผลเทรดปลอม (อ้างเป็นผู้รับ) ไปที่ Host ผ่าน RPC บน avatar ของผู้เล่นอื่น
        string HackTrade(ulong giver, int localId)
        {
            foreach (var r in FindObjectsByType<NisitSimulator.Net.TradeRelay>())
            {
                if (r.OwnerClientId != giver) continue;
                var m = typeof(NisitSimulator.Net.TradeRelay).GetMethod("ResultServerRpc", BindingFlags.NonPublic | BindingFlags.Instance);
                m.Invoke(r, new object[] { giver, localId, true, default(ServerRpcParams) });
                return $"forged ResultServerRpc(giver={giver}, id={localId}, ok=true) sent";
            }
            return "giver relay not found";
        }

        string Rtt()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return "not connected";
            var sb = new StringBuilder();
            if (nm.IsServer) foreach (var id in nm.ConnectedClientsIds) { if (id != nm.LocalClientId) sb.Append($"client{id}={nm.NetworkConfig.NetworkTransport.GetCurrentRtt(id)}ms "); }
            else sb.Append($"server={nm.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId)}ms");
            return sb.ToString();
        }

        static GameObject Player => GameObject.Find("Player");

        string Teleport(Vector3 p)
        {
            var pl = Player; if (pl == null) return "no Player";
            var cc = pl.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            pl.transform.position = p;
            if (cc) cc.enabled = true;
            return "at " + p;
        }

        // เดินด้วย CharacterController เหมือนการกดปุ่มเดิน (ใช้ความเร็ว/ตัวคูณหมดแรงของ PlayerMovement)
        IEnumerator Walk(float dx, float dz, float seconds, bool run)
        {
            var pl = Player; if (pl == null) yield break;
            var cc = pl.GetComponent<CharacterController>();
            var mv = pl.GetComponent<NisitSimulator.Player.PlayerMovement>();
            var ex = pl.GetComponent<NisitSimulator.Player.PlayerExhaustion>();
            var anim = pl.GetComponentInChildren<Animator>();
            var dir = new Vector3(dx, 0f, dz).normalized;
            bool canRun = run && (ex == null || ex.CanRun);
            float speed = (mv != null ? (canRun ? mv.runSpeed : mv.walkSpeed) : 4f) * (ex != null ? ex.MoveSpeedMultiplier : 1f);
            Vector3 start = pl.transform.position;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (cc != null && cc.enabled) cc.Move((dir * speed + Vector3.down * 2f) * Time.deltaTime);
                pl.transform.rotation = Quaternion.LookRotation(dir);
                if (anim != null) anim.SetFloat("Speed", speed);
                yield return null;
            }
            if (anim != null) anim.SetFloat("Speed", 0f);
            Log($"walk done run={run} canRun={canRun} speed={speed:0.00} moved={(pl.transform.position - start).magnitude:0.00}m");
        }

        // ส่ง Named Message ดิบ (ทดสอบว่าเครื่องที่ไม่มีสิทธิ์ส่งข้อความควบคุมแล้ว Host ปฏิเสธ)
        string RawNamedMessage(string[] a)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return "not connected";
            string name = a[0];
            using (var w = new FastBufferWriter(64, Unity.Collections.Allocator.Temp))
            {
                for (int i = 1; i < a.Length; i++)
                {
                    var v = a[i];
                    if (v.StartsWith("i:")) w.WriteValueSafe(int.Parse(v.Substring(2)));
                    else if (v.StartsWith("f:")) w.WriteValueSafe(float.Parse(v.Substring(2), CultureInfo.InvariantCulture));
                    else if (v.StartsWith("b:")) w.WriteValueSafe(bool.Parse(v.Substring(2)));
                }
                if (nm.IsServer) nm.CustomMessagingManager.SendNamedMessageToAll(name, w, NetworkDelivery.ReliableSequenced);
                else nm.CustomMessagingManager.SendNamedMessage(name, NetworkManager.ServerClientId, w, NetworkDelivery.ReliableSequenced);
            }
            return "sent " + name;
        }

        // ---------- reflection: call/get/set บน API สาธารณะของเกม ----------
        //   รูปแบบ  Type.Member  หรือ  Player:Component.Member (คอมโพเนนต์บน Player)
        static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] ts; try { ts = asm.GetTypes(); } catch { continue; }
                foreach (var t in ts) if (t.Name == name || t.FullName == name) return t;
            }
            return null;
        }

        [ThreadStatic] static string targetName;   // จาก Type@ชื่อGameObject.Member

        static object ResolveTarget(Type t, bool onPlayer)
        {
            if (onPlayer) { var pl = Player; return pl != null ? pl.GetComponent(t) : null; }
            if (!string.IsNullOrEmpty(targetName) && typeof(Component).IsAssignableFrom(t))
            {
                foreach (var o in FindObjectsByType(t, FindObjectsInactive.Include))
                    if (((Component)o).gameObject.name == targetName) return o;
                return null;
            }
            var inst = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                       ?? t.GetProperty("Local", BindingFlags.Public | BindingFlags.Static);
            if (inst != null) { var v = inst.GetValue(null); if (v != null) return v; }
            if (typeof(UnityEngine.Object).IsAssignableFrom(t)) return FindAnyObjectByType(t);
            return null;
        }

        static void Split(string spec, out Type t, out string member, out bool onPlayer)
        {
            onPlayer = spec.StartsWith("Player:");
            if (onPlayer) spec = spec.Substring(7);
            int dot = spec.LastIndexOf('.');
            string typeName = spec.Substring(0, dot);
            targetName = null;
            int at = typeName.IndexOf('@');
            if (at >= 0) { targetName = typeName.Substring(at + 1).Replace("_", " "); typeName = typeName.Substring(0, at); }
            t = FindType(typeName);
            member = spec.Substring(dot + 1);
            if (t == null) throw new Exception("type not found: " + typeName);
        }

        string Call(string spec, string[] args)
        {
            Split(spec, out var t, out var member, out var onPlayer);
            var ms = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                      .Where(m => m.Name == member && m.GetParameters().Count(p => !p.IsOptional && !p.IsOut) <= args.Length && m.GetParameters().Count(p => !p.IsOut) >= args.Length)
                      .ToList();
            if (ms.Count == 0) return "method not found: " + spec + "/" + args.Length;
            var m0 = ms[0];
            object target = m0.IsStatic ? null : ResolveTarget(t, onPlayer);
            if (!m0.IsStatic && target == null) return "no instance of " + t.Name;
            var ps = m0.GetParameters();
            var vals = new object[ps.Length];
            int ai = 0;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].IsOut) { vals[i] = null; continue; }
                vals[i] = ai < args.Length ? Convert(args[ai++], ps[i].ParameterType) : ps[i].DefaultValue;
            }
            var r = m0.Invoke(target, vals);
            var sb = new StringBuilder(Describe(r));
            for (int i = 0; i < ps.Length; i++) if (ps[i].IsOut) sb.Append($" {ps[i].Name}={Describe(vals[i])}");
            return sb.ToString();
        }

        static object GetMember(string spec)
        {
            Split(spec, out var t, out var member, out var onPlayer);
            const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var p = t.GetProperty(member, bf); var f = t.GetField(member, bf);
            bool isStatic = (p != null && p.GetGetMethod(true).IsStatic) || (f != null && f.IsStatic);
            object target = isStatic ? null : ResolveTarget(t, onPlayer);
            if (p != null) return p.GetValue(target);
            if (f != null) return f.GetValue(target);
            throw new Exception("member not found: " + spec);
        }

        static void SetMember(string spec, string value)
        {
            Split(spec, out var t, out var member, out var onPlayer);
            const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var p = t.GetProperty(member, bf); var f = t.GetField(member, bf);
            bool isStatic = (p != null && p.GetSetMethod(true) != null && p.GetSetMethod(true).IsStatic) || (f != null && f.IsStatic);
            object target = isStatic ? null : ResolveTarget(t, onPlayer);
            if (f != null) f.SetValue(target, Convert(value, f.FieldType));
            else if (p != null) p.SetValue(target, Convert(value, p.PropertyType));
            else throw new Exception("member not found: " + spec);
        }

        static object Convert(string s, Type t)
        {
            if (t == typeof(string)) return s == "null" ? null : s.Replace("_", " ");
            if (t == typeof(int)) return int.Parse(s, CultureInfo.InvariantCulture);
            if (t == typeof(ulong)) return ulong.Parse(s, CultureInfo.InvariantCulture);
            if (t == typeof(float)) return float.Parse(s, CultureInfo.InvariantCulture);
            if (t == typeof(bool)) return bool.Parse(s);
            if (t.IsEnum) return Enum.Parse(t, s);
            if (t == typeof(GameObject)) return Player;
            if (typeof(Component).IsAssignableFrom(t)) return s == "null" ? null : (object)FindAnyObjectByType(t);
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ICollection<>)) return s == "null" ? null : s.Split(',').ToList();
            throw new Exception("cannot convert to " + t.Name);
        }

        static string Describe(object o)
        {
            if (o == null) return "null";
            if (o is string s) return "\"" + s + "\"";
            if (o is float f) return f.ToString("0.###", CultureInfo.InvariantCulture);
            if (o is System.Collections.IEnumerable en && !(o is string))
            {
                var items = new List<string>(); int n = 0;
                foreach (var x in en) { if (n++ < 30) items.Add(DescribeShort(x)); }
                return "[" + string.Join(", ", items) + (n > 30 ? $", ...({n})" : "") + "]";
            }
            return DescribeShort(o);
        }

        static string DescribeShort(object x)
        {
            if (x == null) return "null";
            if (x is UnityEngine.Object uo) { if (uo == null) return "destroyed"; return x.GetType().Name + "(" + uo.name + ")"; }
            var t = x.GetType();
            if (t.IsPrimitive || t.IsEnum || x is string || x is Vector3) return x.ToString();
            var sb = new StringBuilder(t.Name + "{");
            int n = 0;
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (n++ > 10) break;
                var v = f.GetValue(x);
                if (v is System.Collections.ICollection c && !(v is string)) sb.Append($"{f.Name}=#{c.Count} ");
                else sb.Append($"{f.Name}={v} ");
            }
            return sb.ToString().TrimEnd() + "}";
        }

        // ---------- state snapshot ----------
        [Serializable] class AvatarInfo { public string name; public ulong owner; public bool isOwner; public ulong netId; public Vector3 pos; public bool visible; }
        [Serializable]
        class State
        {
            public string id, time, scene, role, gmState, lastSpawn, saveOverride;
            public ulong localClientId; public bool isConnectedClient, isListening;
            public List<ulong> connectedIds = new List<ulong>();
            public List<AvatarInfo> avatars = new List<AvatarInfo>();
            public Vector3 playerPos; public float playerYaw;
            public int money, exp, level; public float energy, health, hunger, stress, satisfaction;
            public List<string> inventory = new List<string>();
            public int day; public float minutes; public string clock; public float timeScale;
            public bool clockFollower, clockAuthoritative, clockSuspended, mpSession;
            public int cameras, audioListeners, networkObjects, playerObjects;
            public bool exhausted; public string restState; public int exhaustEnterCount, completedRests; public float moveMult;
            public string sleepState; public int completedSleeps; public string sleepLast;
            public bool examRoomOpen, examInProgress; public int examRecordCount, examRewardCount;
            public string academic; public int classYear; public float gpa;
            public string disconnectReason; public List<string> recentErrors = new List<string>();
            public bool pausePanel;
        }

        public void WriteState()
        {
            if (string.IsNullOrEmpty(Dir)) return;
            var s = new State { id = Id, time = DateTime.Now.ToString("HH:mm:ss.fff"), scene = SceneManager.GetActiveScene().name };
            try
            {
                var nm = NetworkManager.Singleton;
                s.isListening = nm != null && nm.IsListening;
                s.role = nm == null || !nm.IsListening ? "off" : nm.IsHost ? "host" : nm.IsServer ? "server" : "client";
                if (s.isListening) { s.localClientId = nm.LocalClientId; s.isConnectedClient = nm.IsConnectedClient; s.disconnectReason = nm.DisconnectReason; }
                if (nm != null && nm.IsServer) s.connectedIds = nm.ConnectedClientsIds.ToList();
                foreach (var av in FindObjectsByType<NisitSimulator.Net.NetworkAvatar>())
                {
                    var r = av.GetComponentInChildren<Renderer>();
                    s.avatars.Add(new AvatarInfo { name = av.DisplayName, owner = av.OwnerClientId, isOwner = av.IsOwner, netId = av.NetworkObjectId, pos = av.transform.position, visible = r != null && r.enabled });
                }
                s.networkObjects = FindObjectsByType<NetworkObject>().Length;
                s.playerObjects = FindObjectsByType<NisitSimulator.Stats.PlayerStats>().Length;
                s.cameras = FindObjectsByType<Camera>().Count(c => c.enabled && c.gameObject.activeInHierarchy && c.targetTexture == null);
                s.audioListeners = FindObjectsByType<AudioListener>().Count(l => l.enabled && l.gameObject.activeInHierarchy);

                var pl = Player;
                if (pl != null) { s.playerPos = pl.transform.position; s.playerYaw = pl.transform.eulerAngles.y; }
                var st = pl != null ? pl.GetComponent<NisitSimulator.Stats.PlayerStats>() : null;
                if (st != null) { s.money = st.Money; s.exp = st.Exp; s.energy = st.Energy; s.health = st.Health; s.hunger = st.Hunger; s.stress = st.Stress; s.satisfaction = st.Satisfaction; }
                if (NisitSimulator.Systems.LevelSystem.Instance != null) s.level = NisitSimulator.Systems.LevelSystem.Instance.Level;
                var inv = NisitSimulator.Systems.InventoryManager.Instance;
                if (inv != null) s.inventory = inv.ToSaveList();

                var clock = FindAnyObjectByType<NisitSimulator.TimeSystem.GameClock>();
                if (clock != null) { s.day = clock.Day; s.minutes = clock.TotalMinutes; s.clock = $"D{clock.Day} {clock.Hour:00}:{clock.Minute:00}"; }
                s.timeScale = Time.timeScale;
                s.clockFollower = NisitSimulator.TimeSystem.GameClock.NetworkFollower;
                s.clockAuthoritative = NisitSimulator.TimeSystem.GameClock.NetworkAuthoritative;
                s.clockSuspended = NisitSimulator.TimeSystem.GameClock.Suspended;
                var sync = NisitSimulator.Net.WorldTimeSync.Instance;
                s.mpSession = sync != null && sync.IsMultiplayerSession;

                var gm = NisitSimulator.Core.GameManager.Instance; s.gmState = gm != null ? gm.State.ToString() : "-";
                s.lastSpawn = PlayerSpawnSystem.LastSpawnReason;
                s.saveOverride = SaveSystem.DevPathOverride;

                var ex = pl != null ? pl.GetComponent<NisitSimulator.Player.PlayerExhaustion>() : null;
                if (ex != null) { s.exhausted = ex.IsExhausted; s.restState = ex.State.ToString(); s.exhaustEnterCount = ex.EnterCount; s.completedRests = ex.CompletedRests; s.moveMult = ex.MoveSpeedMultiplier; }
                var sc = NisitSimulator.Interaction.SleepController.Instance;
                if (sc != null) { s.sleepState = sc.State.ToString(); s.completedSleeps = sc.CompletedSleeps; s.sleepLast = sc.LastResult; }
                var ctl = NisitSimulator.Academics.ExamMinigame.ExamMinigameController.Instance;
                if (ctl != null) { s.examRoomOpen = ctl.IsRoomOpen; s.examInProgress = ctl.HasSessionInProgress; }
                s.examRecordCount = NisitSimulator.Academics.ExamMinigame.ExamMinigameController.DevRecordCount;
                s.examRewardCount = NisitSimulator.Academics.ExamMinigame.ExamMinigameController.DevRewardCount;
                var reg = NisitSimulator.Academics.CourseRegistrar.Instance;
                if (reg != null && reg.Record != null) { s.academic = reg.ShortSummary(); s.classYear = reg.ClassYear; }
                var pm = FindAnyObjectByType<NisitSimulator.UI.PauseMenu>();
                s.pausePanel = pm != null && pm.panel != null && pm.panel.activeSelf;
            }
            catch (Exception e) { s.recentErrors.Add("state: " + e.Message); }
            s.recentErrors.AddRange(recentErrors);
            try { File.WriteAllText(StatePath, JsonUtility.ToJson(s, true), Encoding.UTF8); } catch { }
        }

        // ---------- helpers ----------
        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        static int I(string s) => int.Parse(s, CultureInfo.InvariantCulture);

        static List<string> Tokenize(string line)
        {
            var r = new List<string>(); var cur = new StringBuilder(); bool q = false;
            foreach (var ch in line)
            {
                if (ch == '"') { q = !q; continue; }
                if (ch == ' ' && !q) { if (cur.Length > 0) { r.Add(cur.ToString()); cur.Clear(); } continue; }
                cur.Append(ch);
            }
            if (cur.Length > 0) r.Add(cur.ToString());
            return r;
        }
    }
}
#endif
