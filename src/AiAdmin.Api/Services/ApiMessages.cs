namespace AiAdmin.Api.Services;

public static class ApiMessages
{
    private static readonly Dictionary<string, (string Zh, string En)> _messages = new()
    {
        ["ok"] = ("操作成功", "Operation succeeded")
        , ["loginSuccess"] = ("登录成功", "Login successful")
        , ["invalidCredentials"] = ("用户名或密码错误", "Invalid username or password")
        , ["userExists"] = ("用户名已存在", "Username already exists")
        , ["passwordRequired"] = ("新增用户时密码必填", "Password is required for a new user")
        , ["invalidRole"] = ("包含无效角色", "One or more roles are invalid")
        , ["userCreated"] = ("用户创建成功", "User created")
        , ["userNotFound"] = ("用户不存在", "User not found")
        , ["userUpdated"] = ("用户更新成功", "User updated")
        , ["cannotDeleteSelf"] = ("不能删除当前登录用户", "You cannot delete your own account")
        , ["userDeleted"] = ("用户删除成功", "User deleted")
        , ["roleSuper"] = ("超级管理员", "Super administrator")
        , ["roleAdmin"] = ("管理员", "Administrator")
        , ["roleUser"] = ("普通用户", "User")
    };

    public static string Get(
        HttpRequest request
        , string key
    ) {
        var value = _messages[key];
        return request.Headers.AcceptLanguage.ToString().StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? value.Zh : value.En;
    }
}