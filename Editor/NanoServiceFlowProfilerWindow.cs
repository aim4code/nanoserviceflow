using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aim4code.NanoServiceFlow.Editor
{
    public class NanoServiceFlowProfilerWindow : EditorWindow
    {
        private class LoggedAction
        {
            public DateTime Time;
            public IAction Action;
            public StackTrace Trace;
        }

        private List<LoggedAction> _loggedActions = new List<LoggedAction>();
        private VisualElement _servicesList;
        private VisualElement _actionsList;
        private Label _actionDetailsText;
        private ScrollView _servicesScrollView;
        
        private VisualElement _selectedActionElement;

        private double _nextPollTime;

        [MenuItem("Aim4code/NanoServiceFlow Profiler")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<NanoServiceFlowProfilerWindow>();
            wnd.titleContent = new GUIContent("Flow Profiler");
        }

        private void OnEnable()
        {
            ServiceLocator.EditorIsProfilerActive = true;
            ServiceLocator.OnStateRegistered += HandleStateChange;
            ServiceLocator.OnStateCleared += HandleStateCleared;
            ServiceLocator.OnDispatchStart += HandleDispatchStart;
            
            EditorApplication.update += PerformUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            ServiceLocator.EditorIsProfilerActive = false;
            ServiceLocator.OnStateRegistered -= HandleStateChange;
            ServiceLocator.OnStateCleared -= HandleStateCleared;
            ServiceLocator.OnDispatchStart -= HandleDispatchStart;
            
            EditorApplication.update -= PerformUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Clear profiler when leaving play mode to not hold stale data
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                HandleStateCleared();
            }
        }

        private void PerformUpdate()
        {
            // Update live values every second to not spam reflection
            if (EditorApplication.timeSinceStartup > _nextPollTime)
            {
                _nextPollTime = EditorApplication.timeSinceStartup + 1.0;
                RefreshServicesView();
            }
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.AddToClassList("container");

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.aim4code.nanoserviceflow/Editor/NanoServiceFlowProfilerWindow.uss");
            // If the package is actually inside Packages/com... local or via git, the path could vary. Using find to be safe or explicit:
            if (styleSheet == null)
            {
                var guids = AssetDatabase.FindAssets("NanoServiceFlowProfilerWindow t:StyleSheet");
                if (guids.Length > 0)
                {
                    styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);


            // --- Left Pane Component ---
            var leftPane = new VisualElement();
            leftPane.AddToClassList("pane");
            leftPane.AddToClassList("left-pane");

            var servicesHeader = new Label("Active Services & States");
            servicesHeader.AddToClassList("header");
            leftPane.Add(servicesHeader);

            _servicesScrollView = new ScrollView();
            _servicesScrollView.AddToClassList("scroll-view");
            _servicesList = new VisualElement();
            _servicesScrollView.Add(_servicesList);
            leftPane.Add(_servicesScrollView);


            // --- Right Pane Component ---
            var rightPane = new VisualElement();
            rightPane.AddToClassList("pane");
            rightPane.AddToClassList("right-pane");

            var timelineArea = new VisualElement();
            timelineArea.AddToClassList("timeline-area");

            var actionsHeader = new Label("Actions Timeline");
            actionsHeader.AddToClassList("header");
            timelineArea.Add(actionsHeader);

            var controls = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 5 } };
            var clearBtn = new Button(() => {
                _loggedActions.Clear();
                _actionsList.Clear();
                _actionDetailsText.text = "Select an action to view details.";
            }) { text = "Clear Logs" };
            controls.Add(clearBtn);
            timelineArea.Add(controls);

            var actionsScrollView = new ScrollView();
            actionsScrollView.AddToClassList("scroll-view");
            _actionsList = new VisualElement();
            actionsScrollView.Add(_actionsList);
            timelineArea.Add(actionsScrollView);

            var detailsArea = new VisualElement();
            detailsArea.AddToClassList("details-area");
            
            var detailsHeader = new Label("Action Details");
            detailsHeader.AddToClassList("header");
            detailsArea.Add(detailsHeader);
            
            var detailsScrollView = new ScrollView();
            _actionDetailsText = new Label("Select an action to view details.");
            _actionDetailsText.AddToClassList("details-text");
            detailsScrollView.Add(_actionDetailsText);
            detailsArea.Add(detailsScrollView);

            rightPane.Add(timelineArea);
            rightPane.Add(detailsArea);

            root.Add(leftPane);
            root.Add(rightPane);

            // Initial paint
            RefreshServicesView();
            RedrawActions();
        }

        private void HandleStateChange(Type t, object instance)
        {
            // Force redraw immediately
            RefreshServicesView();
        }

        private void HandleStateCleared()
        {
            _servicesList?.Clear();
            _loggedActions.Clear();
            _actionsList?.Clear();
            if (_actionDetailsText != null) _actionDetailsText.text = "Select an action to view details.";
        }

        private void HandleDispatchStart(IAction action, StackTrace stackTrace)
        {
            var entry = new LoggedAction
            {
                Time = DateTime.Now,
                Action = action,
                Trace = stackTrace
            };
            
            _loggedActions.Add(entry);
            
            if (_actionsList != null)
            {
                // Only keep last 200 items max visually to prevent lag
                if (_loggedActions.Count > 200)
                {
                    _loggedActions.RemoveAt(0);
                    if (_actionsList.childCount > 0)
                        _actionsList.RemoveAt(0);
                }

                AppendAction(entry);
            }
        }

        private void RedrawActions()
        {
            if (_actionsList == null) return;
            _actionsList.Clear();
            foreach (var a in _loggedActions)
            {
                AppendAction(a);
            }
        }

        private void AppendAction(LoggedAction entry)
        {
            var el = new VisualElement();
            el.AddToClassList("action-item");
            
            var timeLbl = new Label(entry.Time.ToString("HH:mm:ss.fff"));
            timeLbl.AddToClassList("action-time");
            el.Add(timeLbl);
            
            var nameLbl = new Label(entry.Action.GetType().Name);
            nameLbl.AddToClassList("action-name");
            el.Add(nameLbl);

            el.RegisterCallback<MouseDownEvent>(e =>
            {
                if (_selectedActionElement != null)
                    _selectedActionElement.RemoveFromClassList("selected");
                
                el.AddToClassList("selected");
                _selectedActionElement = el;
                
                ShowActionDetails(entry);
            });

            _actionsList.Add(el);
            
            // Auto scroll to bottom
            el.RegisterCallback<GeometryChangedEvent>(e => 
            {
                ((ScrollView)_actionsList.parent).ScrollTo(el);
            });
        }

        private void ShowActionDetails(LoggedAction log)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"<color=#B9F6CA>Data Payload:</color>");
            sb.AppendLine(ExtractActionPayload(log.Action));
            sb.AppendLine();
            
            sb.AppendLine($"<color=#FFCC80>Origin / Trace:</color>");
            sb.AppendLine(FormatStackTrace(log.Trace));
            sb.AppendLine();
            
            sb.AppendLine($"<color=#80DEEA>Known Handlers For This Action:</color>");
            var type = log.Action.GetType();
            
            if (ServiceLocator.EditorActionHandlers.TryGetValue(type, out var handlers))
            {
                foreach (var h in handlers)
                {
                    string role = h.IsReducer ? "[Reducer]" : "[SideEffect]";
                    sb.AppendLine($"  {role} -> {h.Target.GetType().Name}.{h.Method.Name}()");
                }
            }
            else
            {
                sb.AppendLine("  (No reducers or side effects registered for this action type)");
            }
            sb.AppendLine();
            
            sb.AppendLine($"<color=#CE93D8>Middlewares Active During Dispatch:</color>");
            if (ServiceLocator.Middlewares.Count > 0)
            {
                foreach(var m in ServiceLocator.Middlewares)
                {
                    sb.AppendLine($"  • {m.GetType().Name}");
                }
            }
            else
            {
                sb.AppendLine("  (No middlewares)");
            }

            // Using rich text for Label in UIElements is slightly restricted, but in generic texts it renders some tags if enableRichText is true (but its property doesn't exist plainly, though text format allows markdown-like or nothing). Wait, rich text works when enableRichText=true? But Label doesn't support full colors. Actually Unity UI Toolkit Label rich text is restricted without style override, so we just use plain text!
            // I should strip the <color> tags or leave them. Let's just use plain text to be safe in Editor UI Toolkit.
            sb.Replace("<color=#B9F6CA>", "=== ").Replace("</color>", " ===");
            sb.Replace("<color=#FFCC80>", "=== ").Replace("<color=#80DEEA>", "=== ").Replace("<color=#CE93D8>", "=== ");
            
            _actionDetailsText.text = sb.ToString();
        }

        private string ExtractActionPayload(IAction action)
        {
            var t = action.GetType();
            var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
            
            List<string> results = new List<string>();
            foreach (var p in props)
            {
                results.Add($"  {p.Name}: {p.GetValue(action)}");
            }
            foreach (var f in fields)
            {
                results.Add($"  {f.Name}: {f.GetValue(action)}");
            }
            
            if (results.Count == 0) return "  (Empty Action)";
            return string.Join("\n", results);
        }

        private string FormatStackTrace(StackTrace trace)
        {
            if (trace == null) return "  (No Trace Available)";
            
            var frames = trace.GetFrames();
            if (frames == null) return "  (No Frames)";

            var sb = new System.Text.StringBuilder();
            int recordedFrames = 0;
            
            for (int i = 0; i < frames.Length; i++)
            {
                var method = frames[i].GetMethod();
                var declType = method.DeclaringType?.Name ?? "Unknown";
                
                // Skip ServiceLocator dispatch itself
                if (declType == "ServiceLocator") continue;
                
                sb.AppendLine($"  at {declType}.{method.Name}()");
                
                // Prevent mega spam
                if (++recordedFrames > 5) 
                {
                    sb.AppendLine("  ...");
                    break;
                }
            }
            
            return sb.ToString();
        }

        private void RefreshServicesView()
        {
            if (_servicesList == null) return;
            
            // To maintain scroll we get current value
            float currentScroll = _servicesScrollView.scrollOffset.y;
            
            _servicesList.Clear();
            var container = ServiceLocator.Container;
            
            if (container.Count == 0)
            {
                _servicesList.Add(new Label("No services or states registered yet."));
            }
            else
            {
                foreach(var kv in container)
                {
                    var type = kv.Key;
                    var instance = kv.Value;

                    var item = new VisualElement();
                    item.AddToClassList("service-item");
                    
                    var title = new Label(type.Name);
                    title.AddToClassList("service-title");
                    item.Add(title);
                    
                    // Fields
                    var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    foreach(var f in fields)
                    {
                         AddRow(item, f.Name, f.GetValue(instance));
                    }

                    // Properties
                    var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    foreach(var p in props)
                    {
                        try 
                        {
                            AddRow(item, p.Name, p.GetValue(instance));
                        } 
                        catch 
                        {
                            AddRow(item, p.Name, "[Error Resolving]");
                        }
                    }

                    _servicesList.Add(item);
                }
            }
        }

        private void AddRow(VisualElement parent, string name, object rawValue)
        {
            var row = new VisualElement();
            row.AddToClassList("service-field");
            
            var n = new Label(name);
            n.AddToClassList("service-field-name");
            row.Add(n);
            
            var v = new Label(ExtractValue(rawValue));
            v.AddToClassList("service-field-value");
            row.Add(v);
            
            parent.Add(row);
        }

        private string ExtractValue(object obj)
        {
            if (obj == null) return "null";
            Type t = obj.GetType();
            
            // Check if it's ReactiveProperty by looking for Value property dynamically
            if (t.IsGenericType && t.Name.Contains("ReactiveProperty"))
            {
                var valProp = t.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                if (valProp != null)
                {
                    var val = valProp.GetValue(obj);
                    return $"{val} (Reactive)";
                }
            }
            
            return obj.ToString();
        }
    }
}
