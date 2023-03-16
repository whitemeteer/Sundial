﻿// MIT 许可证
//
// 版权 © 2020-present 百小僧, 百签科技（广东）有限公司 和所有贡献者
//
// 特此免费授予任何获得本软件副本和相关文档文件（下称“软件”）的人不受限制地处置该软件的权利，
// 包括不受限制地使用、复制、修改、合并、发布、分发、转授许可和/或出售该软件副本，
// 以及再授权被配发了本软件的人如上的权利，须在下列条件下：
//
// 上述版权声明和本许可声明应包含在该软件的所有副本或实质成分中。
//
// 本软件是“如此”提供的，没有任何形式的明示或暗示的保证，包括但不限于对适销性、特定用途的适用性和不侵权的保证。
// 在任何情况下，作者或版权持有人都不对任何索赔、损害或其他责任负责，无论这些追责来自合同、侵权或其它行为中，
// 还是产生于、源于或有关于本软件以及本软件的使用或其它处置。

using Microsoft.AspNetCore.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sundial;

/// <summary>
/// Schedule 模块 UI 中间件
/// </summary>
public sealed class ScheduleUIMiddleware
{
    /// <summary>
    /// 固定请求路径
    /// </summary>
    internal const string REQUEST_PATH = "/schedule";

    private const string API_REQUEST_PATH = $"{REQUEST_PATH}/api";

    /// <summary>
    /// 请求委托
    /// </summary>
    private readonly RequestDelegate _next;

    /// <summary>
    /// 作业计划工厂
    /// </summary>
    private readonly ISchedulerFactory _schedulerFactory;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="next">请求委托</param>
    /// <param name="schedulerFactory">作业计划工厂</param>
    public ScheduleUIMiddleware(RequestDelegate next
        , ISchedulerFactory schedulerFactory)
    {
        _next = next;
        _schedulerFactory = schedulerFactory;
    }

    /// <summary>
    /// 中间件执行方法
    /// </summary>
    /// <param name="context"><see cref="HttpContext"/></param>
    /// <returns><see cref="Task"/></returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // 如果不是以 API_REQUEST_PATH 开头，则跳过
        if (!context.Request.Path.StartsWithSegments(API_REQUEST_PATH))
        {
            await _next(context);
            return;
        }

        // 只处理 GET/POST 请求
        if (context.Request.Method.ToUpper() != "GET" && context.Request.Method.ToUpper() != "POST")
        {
            await _next(context);
            return;
        }

        // 获取匹配的路由标识
        var action = context.Request.Path.Value?[API_REQUEST_PATH.Length..]?.ToLower();

        // 允许跨域，设置返回 json
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        context.Response.Headers["Access-Control-Allow-Headers"] = "*";

        // 路由匹配
        switch (action)
        {
            // 获取所有作业
            case "/get-jobs":
                var jobs = _schedulerFactory.GetJobsOfModels();

                // 输出 JSON
                await context.Response.WriteAsync(SerializeToJson(jobs));
                break;
            // 操作作业
            case "/operate-job":
                // 获取作业 Id
                var jobId = context.Request.Query["jobid"];
                // 获取操作方法
                var operate = context.Request.Query["action"];

                // 获取作业计划
                var scheduleResult = _schedulerFactory.TryGetJob(jobId, out var scheduler);

                // 处理找不到作业情况
                if (scheduleResult != ScheduleResult.Succeed)
                {
                    // 标识状态码为 500
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                    // 输出 JSON
                    await context.Response.WriteAsync(SerializeToJson(new {
                        msg = scheduleResult.ToString(),
                        ok = false
                    }));

                    return;
                }

                switch (operate)
                {
                    // 启动作业
                    case "start":
                        scheduler?.Start();
                        break;
                    // 暂停作业
                    case "pause":
                        scheduler?.Pause();
                        break;
                    // 移除作业
                    case "remove":
                        _schedulerFactory.RemoveJob(jobId);
                        break;
                    // 立即执行
                    case "run":
                        _schedulerFactory.RunJob(jobId);
                        break;
                }

                // 输出 JSON
                await context.Response.WriteAsync(SerializeToJson(new {
                    msg = ScheduleResult.Succeed.ToString(),
                    ok = true
                }));

                break;
            // 操作触发器
            case "/operate-trigger":
                // 获取作业 Id
                var jobId1 = context.Request.Query["jobid"];
                var triggerId = context.Request.Query["triggerid"];
                // 获取操作方法
                var operate1 = context.Request.Query["action"];

                // 获取作业计划
                var scheduleResult1 = _schedulerFactory.TryGetJob(jobId1, out var scheduler1);

                // 处理找不到作业情况
                if (scheduleResult1 != ScheduleResult.Succeed)
                {
                    // 标识状态码为 500
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                    // 输出 JSON
                    await context.Response.WriteAsync(SerializeToJson(new {
                        msg = scheduleResult1.ToString(),
                        ok = false
                    }));

                    return;
                }

                switch (operate1)
                {
                    // 启动作业触发器
                    case "start":
                        scheduler1?.StartTrigger(triggerId);
                        break;
                    // 暂停作业触发器
                    case "pause":
                        scheduler1?.PauseTrigger(triggerId);
                        break;
                    // 移除作业触发器
                    case "remove":
                        scheduler1?.RemoveTrigger(triggerId);
                        break;
                    // 获取作业触发器最近运行时间
                    case "timelines":
                        var trigger = scheduler1?.GetTrigger(triggerId);
                        var timelines = trigger?.GetTimelines() ?? Array.Empty<TriggerTimeline>();

                        // 输出 JSON
                        await context.Response.WriteAsync(SerializeToJson(timelines));
                        return;
                }

                // 输出 JSON
                await context.Response.WriteAsync(SerializeToJson(new {
                    msg = ScheduleResult.Succeed.ToString(),
                    ok = true
                }));

                break;
        }
    }

    /// <summary>
    /// 将对象输出为 JSON 字符串
    /// </summary>
    /// <param name="obj">对象</param>
    /// <returns><see cref="string"/></returns>
    private static string SerializeToJson(object obj)
    {
        // 初始化默认序列化选项
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            AllowTrailingCommas = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // 处理时间类型
        var libraryAssembly = typeof(Schedular).Assembly;
        var dateTimeJsonConverter = Activator.CreateInstance(libraryAssembly.GetType($"{libraryAssembly.GetName().Name}.DateTimeJsonConverter"));
        jsonSerializerOptions.Converters.Add(dateTimeJsonConverter as JsonConverter);

        return JsonSerializer.Serialize(obj, jsonSerializerOptions);
    }
}