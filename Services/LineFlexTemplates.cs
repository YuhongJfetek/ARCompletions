namespace ARCompletions.Services
{
    public static class LineFlexTemplates
    {
        public static object MainMenu() => new
        {
            type = "bubble",
            body = new
            {
                type = "box",
                layout = "vertical",
                contents = new object[]
                {
                    new { type = "text", text = "主選單", weight = "bold", size = "xl" },
                    new { type = "text", text = "請選擇：", size = "sm", color = "#666666", margin = "md" }
                }
            },
            footer = new
            {
                type = "box",
                layout = "vertical",
                spacing = "sm",
                contents = new object[]
                {
                    new {
                        type = "button",
                        style = "primary",
                        action = new { type = "postback", label = "客服", data = "action=help" }
                    },
                    new {
                        type = "button",
                        style = "secondary",
                        action = new { type = "postback", label = "查訂單", data = "action=check_order" }
                    }
                }
            }
        };
    }
}
