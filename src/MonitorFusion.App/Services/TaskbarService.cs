using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using MonitorFusion.App.Views;
using MonitorFusion.Core.Models;
using MonitorFusion.Core.Services;

namespace MonitorFusion.App.Services;

/// <summary>
/// Manages the creation and lifecycle of custom taskbars on secondary monitors.
/// </summary>
public class TaskbarService
{
    private readonly MonitorDetectionService _monitorService;
    private readonly SettingsService _settingsService;
    private readonly Dictionary<string, TaskbarWindow> _activeTaskbars = new();
    private bool _isRunning;

    public TaskbarService(MonitorDetectionService monitorService, SettingsService settingsService)
    {
        _monitorService = monitorService;
        _settingsService = settingsService;

        // Listen for monitor changes to add/remove taskbars dynamically
        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        
        RefreshTaskbars();
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;

        foreach (var taskbar in _activeTaskbars.Values)
        {
            taskbar.UnregisterAppBar();
            taskbar.Close();
        }
        _activeTaskbars.Clear();
    }

    public void ReloadSettings()
    {
        // Simply restart to apply new settings (height, position, enabled state)
        Stop();
        
        var settings = _settingsService.Load();
        if (settings.Taskbar.Enabled)
        {
            Start();
        }
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (_isRunning)
        {
            // Re-evaluate monitors on changes (plugging/unplugging)
            Application.Current.Dispatcher.Invoke(RefreshTaskbars);
        }
    }

    private void RefreshTaskbars()
    {
        var settings = _settingsService.Load().Taskbar;
        if (!settings.Enabled)
        {
            Stop();
            return;
        }

        var currentMonitors = _monitorService.GetAllMonitors();
        
        // Remove taskbars for monitors that no longer exist or shouldn't have one
        var monitorsToRemove = _activeTaskbars.Keys
            .Where(id => !currentMonitors.Any(m => m.DeviceId == id) || 
                         (currentMonitors.First(m => m.DeviceId == id).IsPrimary && !settings.ShowOnAllMonitors))
            .ToList();

        foreach (var id in monitorsToRemove)
        {
            _activeTaskbars[id].UnregisterAppBar();
            _activeTaskbars[id].Close();
            _activeTaskbars.Remove(id);
        }

        // Add taskbars for new eligible monitors
        foreach (var monitor in currentMonitors)
        {
            // Skip primary monitor unless specifically requested
            if (monitor.IsPrimary && !settings.ShowOnAllMonitors)
                continue;

            if (!_activeTaskbars.ContainsKey(monitor.DeviceId))
            {
                var taskbar = new TaskbarWindow(monitor, settings);
                taskbar.Show();
                _activeTaskbars[monitor.DeviceId] = taskbar;
            }
        }
    }
}
