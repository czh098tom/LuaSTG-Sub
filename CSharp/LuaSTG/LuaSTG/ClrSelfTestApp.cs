using System;
using System.IO;
using System.Text;
using LuaSTG.Core;

namespace LuaSTG
{
    /// <summary>
    /// CoreCLR 绑定自测应用。通过命令行 --clr-selftest 启用。
    /// 结果写入 clr_selftest_result.txt，FrameFunc 返回 true 请求退出。
    /// 附加 --lua-sync 时与 Lua 测试脚本（tool/clr-selftest-lua/main.lua）做双向数据同步验证。
    /// </summary>
    public sealed class ClrSelfTestApp : ILuaSTGApp
    {
        private readonly StringBuilder _results = new();
        private readonly bool _luaSync;
        private int _failures;
        private int _frame;
        private int _stage;

        public ClrSelfTestApp(bool luaSync = false)
        {
            _luaSync = luaSync;
        }

        private void Check(bool condition, string name, string detail = "")
        {
            var line = condition ? $"PASS {name}" : $"FAIL {name} {detail}";
            _results.AppendLine(line);
            LuaSTGAPI.Log(condition ? LogLevel.Info : LogLevel.Error, $"[selftest] {line}");
            if (!condition)
            {
                _failures++;
            }
        }

        private void Finish()
        {
            var result = _failures == 0 ? "PASS" : "FAIL";
            _results.AppendLine($"RESULT {result}");
            File.WriteAllText("clr_selftest_result.txt", _results.ToString());
            LuaSTGAPI.Log(LogLevel.Info, $"[selftest] 完成：{_failures} 项失败");
        }

        public void GameInit()
        {
            try
            {
                if (_luaSync)
                {
                    _stage = 10; // Lua 同步模式：等待 Lua 创建对象
                    return;
                }
                RunInitTests();
                RunGameObjectTests();
            }
            catch (Exception e)
            {
                Check(false, "GameInit 异常", e.ToString());
                Finish();
                _stage = -1; // 直接退出
            }
        }

        private void RunInitTests()
        {
            Check(LuaSTGAPI.VersionMajor != 0 || LuaSTGAPI.VersionMinor != 0 || LuaSTGAPI.VersionPatch != 0,
                "版本号可读", $"{LuaSTGAPI.VersionMajor}.{LuaSTGAPI.VersionMinor}.{LuaSTGAPI.VersionPatch}");
            Check(!string.IsNullOrEmpty(LuaSTGAPI.VersionName), "版本名可读", LuaSTGAPI.VersionName);
            Check(LuaSTGAPI.ObjectPoolSize == 32768, "对象池容量", $"{LuaSTGAPI.ObjectPoolSize}");
            Check(GameObjectManager.GetPoolCapacity() == 32768, "对象池容量 API");
            LuaSTGAPI.SetFPS(60);
            Check(Math.Abs(LuaSTGAPI.GetFPS()) < 1e6, "FPS 可读", $"{LuaSTGAPI.GetFPS()}");
            LuaSTGAPI.SetTitle("LuaSTG CoreCLR SelfTest");
        }

        private sealed class TestBullet : GameObjectBase
        {
            public int FrameCalls;
            public DestroyEventType? DestroyReason;

            public override void OnFrame()
            {
                FrameCalls++;
            }

            public override void OnDestroy(DestroyEventArgs args)
            {
                DestroyReason = args.DestroyEventType;
            }

            public override void OnColli(Collision collision)
            {
            }
        }

        private void RunGameObjectTests()
        {
            // 1. 无参构造分配引擎对象
            var bullet = new TestBullet();
            Check(bullet.IsValid, "构造分配对象");
            Check(bullet.Status == GameObjectStatus.Active, "初始状态 Active");

            // 2. 引擎数据写入读出
            bullet.X = 123.5;
            bullet.Y = -42.25;
            bullet.Vx = 3.0;
            bullet.Layer = 0.5;
            bullet.Rot = 90.0; // 角度制
            Check(Math.Abs(bullet.X - 123.5) < 1e-9 && Math.Abs(bullet.Y + 42.25) < 1e-9, "坐标读写");
            Check(Math.Abs(bullet.Rot - 90.0) < 1e-9, "角度读写");
            bullet.Group = 7;
            Check(bullet.Group == 7, "碰撞组读写");
            bullet.Hide = true;
            Check(bullet.Hide, "Hide 标志");
            bullet.Timer = 99;
            Check(bullet.Timer == 99, "Timer 读写");

            // 3. Delete 后访问抛异常
            bullet.Delete();
            Check(bullet.IsDestroyed, "Delete 后 IsDestroyed");
            var threw = false;
            try { _ = bullet.X; }
            catch (ObjectDisposedException) { threw = true; }
            Check(threw, "Delete 后访问引擎数据抛异常");
            Check(bullet.DestroyReason == DestroyEventType.Del, "Delete 触发 OnDestroy(Del)", $"{bullet.DestroyReason}");

            // 4. 引擎数据可从引擎侧访问（对象仍在池中，AfterFrame 后回收）
            var countBefore = GameObjectManager.GetObjectCount();
            Check(countBefore >= 1, "对象计数", $"{countBefore}");

            // 5. OnFrame 回调分发
            var mover = new TestBullet();
            _frame = 0;
            _stage = 1;
            _mover = mover;
        }

        private TestBullet? _mover;

        private void RunLuaSyncTests()
        {
            // Lua 在 GameInit 创建了 3 个对象：x=i*10, y=40+i, group=2, vx=0.5*i
            var count = 0;
            var matched = 0;
            foreach (var o in GameObjectManager.ObjList())
            {
                count++;
                for (var i = 1; i <= 3; i++)
                {
                    if (Math.Abs(o.X - i * 10.0) < 1e-6 && Math.Abs(o.Y - (40.0 + i)) < 1e-6
                        && o.Group == 2 && Math.Abs(o.Vx - 0.5 * i) < 1e-6)
                    {
                        matched++;
                        break;
                    }
                }
            }
            Check(count >= 3, "ObjList 看到 Lua 创建的对象", $"{count}");
            Check(matched == 3, "Lua 写入的引擎数据在 C# 侧可读且同步", $"{matched}/3");

            // C# 创建对象并写引擎数据，供 Lua 侧校验（Lua 在 frame 12 检查）
            var cs = new TestBullet();
            cs.X = 777.0;
            cs.Y = 888.0;
            Check(cs.IsValid, "C# 在 Lua 侧对象池中创建对象");
        }

        private void CheckLuaResult()
        {
            try
            {
                if (File.Exists("lua_sync_result.txt"))
                {
                    var text = File.ReadAllText("lua_sync_result.txt");
                    Check(text.StartsWith("PASS"), "Lua 侧校验 C# 对象数据同步", text.Trim());
                    Finish();
                    _stage = -1;
                    return;
                }
            }
            catch (Exception e)
            {
                Check(false, "读取 lua_sync_result 异常", e.ToString());
                Finish();
                _stage = -1;
                return;
            }
            // Lua 结果文件尚未产生，继续等待
        }

        public bool FrameFunc()
        {
            if (_stage == -1)
            {
                return true;
            }
            _frame++;
            try
            {
                if (_stage == 10)
                {
                    // Lua 同步模式
                    if (_frame == 10)
                    {
                        RunLuaSyncTests();
                    }
                    if (_frame is 14 or 20 or 30)
                    {
                        CheckLuaResult();
                    }
                    if (_frame > 60)
                    {
                        Check(false, "Lua 同步超时（lua_sync_result.txt 未产生）");
                        Finish();
                        _stage = -1;
                    }
                    return false;
                }

                switch (_stage)
                {
                    case 1: // 验证回调分发与引擎运动更新
                        if (_frame == 3)
                        {
                            Check(_mover!.FrameCalls >= 1, "OnFrame 回调被引擎调用", $"{_mover.FrameCalls}");
                            _mover.Vx = 2.0;
                            _mover.Vy = 0;
                        }
                        if (_frame == 5)
                        {
                            Check(Math.Abs(_mover!.X - 2.0 * 2) < 1e-6, "引擎运动更新生效", $"{_mover.X}");
                            var found = 0;
                            foreach (var o in GameObjectManager.ObjList())
                            {
                                found++;
                                _ = o.Id;
                            }
                            Check(found >= 1, "ObjList 迭代", $"{found}");
                        }
                        if (_frame == 6)
                        {
                            GameObjectManager.ObjFrame();
                            GameObjectManager.AfterFrame(); // 回收 Del/Kill 对象
                        }
                        if (_frame == 8)
                        {
                            Check(!_mover!.IsValid || _mover.IsDestroyed, "回收后包装解除");
                            GameObjectManager.ResetPool();
                            Finish();
                            _stage = -1;
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                Check(false, $"FrameFunc 阶段 {_stage} 异常", e.ToString());
                Finish();
                _stage = -1;
            }
            // 超时保护
            if (_frame > 300)
            {
                Check(false, "超时", $"{_frame}");
                Finish();
                return true;
            }
            return false;
        }

        public void RenderFunc()
        {
            // 渲染回调能正常调用基础渲染 API
            if (!_luaSync && _frame == 4 && LuaSTGAPI.BeginScene())
            {
                LuaSTGAPI.RenderClear(255, 32, 64, 128);
                LuaSTGAPI.EndScene();
            }
        }

        public void GameExit()
        {
        }

        public void FocusGainFunc()
        {
        }

        public void FocusLoseFunc()
        {
        }

        public void EventFunc(EngineEvent eventType, bool state)
        {
        }
    }
}
