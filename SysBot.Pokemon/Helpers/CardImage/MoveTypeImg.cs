using System.Collections.Generic;

namespace SysBot.Pokemon;

public class MoveTypeImg
{
    #region 技能属性图片

    public static Dictionary<int, string> MoveTypeUrlMapping = new Dictionary<int, string>
    {
        {0, "https://img.kookapp.cn/attachments/2024-05/06/bnRt8D2VkF01o01o.png"},
        {1, "https://img.kookapp.cn/attachments/2024-05/06/PlhJryrlo201o01o.png"},
        {2, "https://img.kookapp.cn/attachments/2024-05/06/NOyqbZn1nw01o01o.png"},
        {3, "https://img.kookapp.cn/attachments/2024-05/06/zgW53sLuGa01o01o.png"},
        {4, "https://img.kookapp.cn/attachments/2024-05/06/wusSQwx8qF01o01o.png"},
        {5, "https://img.kookapp.cn/attachments/2024-05/06/QmpI7XLOyS01o01o.png"},
        {6, "https://img.kookapp.cn/attachments/2024-05/06/yb9j2LyyVD01o01o.png"},
        {7, "https://img.kookapp.cn/attachments/2024-05/06/Heb7gaqn3L01o01o.png"},
        {8, "https://img.kookapp.cn/attachments/2024-05/06/EuhyqohKsA01o01o.png"},
        {9, "https://img.kookapp.cn/attachments/2024-05/06/ruM4CuPeJR01o01o.png"},
        {10, "https://img.kookapp.cn/attachments/2024-05/06/Pdp6HyUqHG01o01o.png"},
        {11, "https://img.kookapp.cn/attachments/2024-05/06/ooPhJa3bvt01o01o.png"},
        {12, "https://img.kookapp.cn/attachments/2024-05/06/tUIOZqopAT01o01o.png"},
        {13, "https://img.kookapp.cn/attachments/2024-05/06/kkPBqKxdfZ01o01o.png"},
        {14, "https://img.kookapp.cn/attachments/2024-05/06/hERL8WciTc01o01o.png"},
        {15, "https://img.kookapp.cn/attachments/2024-05/06/s6J9dWNxVx01o01o.png"},
        {16, "https://img.kookapp.cn/attachments/2024-05/06/hheSXG1tWw01o01o.png"},
        {17, "https://img.kookapp.cn/attachments/2024-05/06/Rc5P7Z0hP601o01o.png"},
    };

    #endregion 技能属性图片

    public string MoveTypeToChinese(int move)
    {
        if (MoveTypeUrlMapping.ContainsKey(move))
        {
            return MoveTypeUrlMapping[move];
        }
        else
        {
            string errorUrl = "https://img.kookapp.cn/attachments/2024-05/06/JdiY6GZKZm00k00k.png";
            return errorUrl;
        }
    }
}