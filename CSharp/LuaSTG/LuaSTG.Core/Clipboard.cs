using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 剪贴板（对应 Lua 侧 lstg.Clipboard，静态类）。
    /// </summary>
    public static unsafe partial class Clipboard
    {
        /// <summary>剪贴板中是否有文本（对应 lstg.Clipboard.hasText）</summary>
        public static bool HasText()
            => LuaSTGAPI.api.clipboard_hasText() != 0;

        /// <summary>
        /// 读取剪贴板文本（对应 lstg.Clipboard.getText）。
        /// </summary>
        /// <returns>剪贴板文本；读取失败（无权限等）时返回 null（Lua 侧返回 nil）</returns>
        public static string? GetText()
        {
            var ptr = LuaSTGAPI.api.clipboard_getText();
            return ptr == null ? null : StringMarshal.FromUtf8(ptr);
        }

        /// <summary>
        /// 写入剪贴板文本（对应 lstg.Clipboard.setText）。
        /// </summary>
        /// <returns>是否写入成功</returns>
        public static bool SetText(string text)
        {
            using var s = new MarshaledString(text);
            return LuaSTGAPI.api.clipboard_setText(s) != 0;
        }
    }
}
