-- Lua↔C# 同步验证脚本
-- Lua 侧创建对象并写引擎数据，C# 侧（--clr-selftest --lua-sync）读取校验；
-- C# 侧创建对象并写数据，Lua 侧在 frame 20 校验后写 lua_sync_result.txt

local frame = 0

-- 类需要按引擎约定定义全部 6 个回调槽位（init/del/frame/render/colli/kill）
local bullet = {
    function(self) end,
    function(self) end,
    function(self) end,
    function(self) end,
    function(self, other) end,
    function(self) end,
    is_class = true,
}

function GameInit()
    -- 创建 3 个 Lua 对象，引擎数据带已知值
    for i = 1, 3 do
        local o = lstg.New(bullet)
        o.x = i * 10.0
        o.y = 40.0 + i
        o.group = 2
        o.vx = 0.5 * i
    end
end

function FrameFunc()
    frame = frame + 1
    if frame == 12 then
        -- 校验 C# 侧创建的对象（C# 在 frame 5 创建 id 未知，按数量与坐标特征校验）
        local n = 0
        local found = false
        for _, o in lstg.ObjList(16) do
            if o then
                n = n + 1
                -- C# 对象创建于 (777.0, 888.0)，速度 (0,0)
                if math.abs(o.x - 777.0) < 0.001 and math.abs(o.y - 888.0) < 0.001 then
                    found = true
                end
            end
        end
        local f = io.open("lua_sync_result.txt", "w")
        if found and n >= 3 then
            f:write("PASS\n")
        else
            f:write(string.format("FAIL found=%s n=%d\n", tostring(found), n))
        end
        f:close()
    end
    if frame == 40 then
        return true
    end
    return false
end

function RenderFunc()
    lstg.ObjRender()
end
