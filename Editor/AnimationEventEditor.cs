// Copyright (c) 2020 Takanori Shibasaki
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE
// OR OTHER DEALINGS IN THE SOFTWARE.

using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;

class AnimationEventEditor : EditorWindow
{
    static class Styles
    {
        public static readonly GUIStyle ApplyRevertButton;

        static Styles()
        {
            ApplyRevertButton = new GUIStyle(EditorStyles.toolbarButton);
            ApplyRevertButton.fontStyle = FontStyle.Bold;
        }
    }

    static MethodInfo mApplyAndImportMethod;
    static MethodInfo mResetHashMethod;
    static MethodInfo mResetValuesMethod;

    static System.Type mAssetImporterTabbedEditorType;
    static PropertyInfo mActiveTabProperty;
    static FieldInfo mTabsField;
    static FieldInfo mAnimationClipEditorField;

    static System.Type mModelImporterClipEditorType;

    static System.Type mAnimationClipEditorType;
    static FieldInfo mIsDirtyField;
    static FieldInfo mClipField;
    static FieldInfo mClipInfoField;
    static FieldInfo mAvatarPreviewField;

    static System.Type mAnimationClipInfoPropertiesType;
    static MethodInfo mGetEventsMethod;
    static MethodInfo mSetEventsMethod;

    static System.Type mAvatarPreviewType;
    static FieldInfo mTimeControlField;

    static System.Type mTimeControlType;
    static FieldInfo mStartTimeField;
    static FieldInfo mStopTimeField;
    static FieldInfo mCurrentTimeField;

    [System.NonSerialized]
    AssetImporterEditor mRootEditor;

    [System.NonSerialized]
    Editor mAnimationClipEditor;

    [System.NonSerialized]
    float mCurrentTime;

    [System.NonSerialized]
    float mStartTime;

    [System.NonSerialized]
    float mStopTime;

    [System.NonSerialized]
    AnimationClip mClip;

    [System.NonSerialized]
    AnimationEvent[] mEvents = new AnimationEvent[0];

    [System.NonSerialized]
    List<int> mSelectedEvents = new List<int>();

    [System.NonSerialized]
    AnimationEvent[] mCopiedAnimationEvents;

    [SerializeField]
    Vector2 mScroll;

    object mClipInfo;
    System.Action mPostImport;
    bool mRepaintFired;

    [System.Serializable]
    struct SerializedAnimationEvent
    {
        public float Time;
        public string FunctionName;
        public float FloatParameter;
        public int IntParameter;
        public string StringParameter;
        public int ObjectInstanceID;

        public SerializedAnimationEvent(AnimationEvent e)
        {
            Time = e.time;
            FunctionName = e.functionName;
            FloatParameter = e.floatParameter;
            IntParameter = e.intParameter;
            StringParameter = e.stringParameter;
            ObjectInstanceID = e.objectReferenceParameter != null ? e.objectReferenceParameter.GetInstanceID() : 0;
        }

        public AnimationEvent ToAnimationEvent()
        {
            AnimationEvent ev = new AnimationEvent();
            ev.time = Time;
            ev.functionName = FunctionName;
            ev.floatParameter = FloatParameter;
            ev.intParameter = IntParameter;
            ev.stringParameter = StringParameter;
            ev.objectReferenceParameter = ObjectInstanceID != 0 ? EditorUtility.InstanceIDToObject(ObjectInstanceID) : null;
            return ev;
        }
    }

    [System.Serializable]
    class SerializedAnimationEventsJSON
    {
        public SerializedAnimationEvent[] Events = new SerializedAnimationEvent[0];
    }

    static AnimationEventEditor()
    {
        mApplyAndImportMethod = typeof(AssetImporterEditor).GetMethod("ApplyAndImport", BindingFlags.Instance | BindingFlags.NonPublic);
        mResetHashMethod = typeof(AssetImporterEditor).GetMethod("ResetHash", BindingFlags.Instance | BindingFlags.NonPublic, null, new System.Type[0], null);
        mResetValuesMethod = typeof(AssetImporterEditor).GetMethod("ResetValues", BindingFlags.Instance | BindingFlags.NonPublic);

        mAssetImporterTabbedEditorType = typeof(AssetImporterEditor).Assembly.GetType("UnityEditor.AssetImporterTabbedEditor");
        mActiveTabProperty = mAssetImporterTabbedEditorType.GetProperty("activeTab", BindingFlags.Instance | BindingFlags.Public);
        mTabsField = mAssetImporterTabbedEditorType.GetField("m_Tabs", BindingFlags.Instance | BindingFlags.NonPublic);

        mModelImporterClipEditorType = typeof(AssetImporterEditor).Assembly.GetType("UnityEditor.ModelImporterClipEditor");
        mAnimationClipEditorField = mModelImporterClipEditorType.GetField("m_AnimationClipEditor", BindingFlags.Instance | BindingFlags.NonPublic);

        mAnimationClipEditorType = typeof(EditorWindow).Assembly.GetType("UnityEditor.AnimationClipEditor");
        mClipField = mAnimationClipEditorType.GetField("m_Clip", BindingFlags.Instance | BindingFlags.NonPublic);
        mClipInfoField = mAnimationClipEditorType.GetField("m_ClipInfo", BindingFlags.Instance | BindingFlags.NonPublic);
        mAvatarPreviewField = mAnimationClipEditorType.GetField("m_AvatarPreview", BindingFlags.Instance | BindingFlags.NonPublic);

        mAnimationClipInfoPropertiesType = typeof(EditorWindow).Assembly.GetType("UnityEditor.AnimationClipInfoProperties");
        mGetEventsMethod = mAnimationClipInfoPropertiesType.GetMethod("GetEvents", BindingFlags.Instance | BindingFlags.Public);
        mSetEventsMethod = mAnimationClipInfoPropertiesType.GetMethod("SetEvents", BindingFlags.Instance | BindingFlags.Public);

        mAvatarPreviewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.AvatarPreview");
        mTimeControlField = mAvatarPreviewType.GetField("timeControl", BindingFlags.Instance | BindingFlags.Public);

        mTimeControlType = typeof(EditorWindow).Assembly.GetType("UnityEditor.TimeControl");
        mStartTimeField = mTimeControlType.GetField("startTime", BindingFlags.Instance | BindingFlags.Public);
        mStopTimeField = mTimeControlType.GetField("stopTime", BindingFlags.Instance | BindingFlags.Public);
        mCurrentTimeField = mTimeControlType.GetField("currentTime", BindingFlags.Instance | BindingFlags.Public);
    }

    [MenuItem("Window/Animation/Animation Events")]
    static void Open()
    {
        GetWindow<AnimationEventEditor>().Show();
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Animation Events");
    }

    private void OnDisable()
    {
    }

    private void OnSelectionChange()
    {
        Repaint();
    }

    AnimationEvent[] GetCopiedAnimatioEvents()
    {
        string json = EditorGUIUtility.systemCopyBuffer;
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var events = JsonUtility.FromJson<SerializedAnimationEventsJSON>(json);
                if (events.Events != null)
                {
                    AnimationEvent[] result = new AnimationEvent[events.Events.Length];
                    for (int i = 0; i < result.Length; ++i)
                    {
                        result[i] = events.Events[i].ToAnimationEvent();
                    }
                    return result;
                }
            }
            catch (System.Exception)
            {
            }
        }
        return null;
    }

    static Editor GetAnimationClipEditor()
    {
        foreach (var obj in Resources.FindObjectsOfTypeAll(mAnimationClipEditorType))
        {
            return obj as Editor;
        }
        return null;
    }

    static AnimationEvent[] GetEvents(object animationClipInfoProperties)
    {
        return mGetEventsMethod.Invoke(animationClipInfoProperties, null) as AnimationEvent[];
    }

    static void SetEvents(object animationClipInfoProperties, AnimationEvent[] events)
    {
        mSetEventsMethod.Invoke(animationClipInfoProperties, new object[] { events });
    }

    private void OnGUI()
    {
        if (mPostImport != null && Event.current.type == EventType.Repaint)
        {
            mRepaintFired = true;
        }

        mAnimationClipEditor = GetAnimationClipEditor();

        if (mAnimationClipEditor == null)
        {
            return;
        }

        var clipInfo = mClipInfoField.GetValue(mAnimationClipEditor);

        if (clipInfo == null)
        {
            return;
        }

        if (mClipInfo != clipInfo)
        {
            mClipInfo = clipInfo;
            OnClipSelect();
        }

        if (!GetTime(out mStartTime, out mStopTime, out mCurrentTime))
        {
            return;
        }

        ToolbarGUI();

        GUI.changed = false;

        mScroll = EditorGUILayout.BeginScrollView(mScroll, false, false, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUIStyle.none);

        for (int i = 0; i < mEvents.Length; ++i)
        {
            EventGUI(i);
        }

        EditorGUILayout.EndScrollView();

        if (GUI.changed)
        {
            ApplyChanges();
        }

        if (Event.current.type == EventType.MouseDown)
        {
            GUI.FocusControl(null);
            mSelectedEvents.Clear();
            Event.current.Use();
            Repaint();
        }
    }

    private void Update()
    {
        if (mClipInfo != null)
        {
            Repaint();
        }

        if (mPostImport != null && mRepaintFired)
        {
            mRepaintFired = false;
            mPostImport();
            mPostImport = null;
        }
    }

    AssetImporterEditor GetRootEditor()
    {
        foreach (var editor in Resources.FindObjectsOfTypeAll<AssetImporterEditor>())
        {
            if (editor.GetType().IsSubclassOf(mAssetImporterTabbedEditorType))
            {
                object[] tabs = mTabsField.GetValue(editor) as object[];

                foreach (var tab in tabs)
                {
                    if (tab.GetType().IsAssignableFrom(mModelImporterClipEditorType))
                    {
                        var clipEditor = mAnimationClipEditorField.GetValue(tab) as Object;

                        if (clipEditor == mAnimationClipEditor)
                        {
                            return editor;
                        }
                        break;
                    }
                }
            }
        }

        return null;
    }

    void ApplyChanges()
    {
        SetEvents(mClipInfo, mEvents);

        if (mRootEditor != null && mRootEditor.HasModified())
        {
            mAnimationClipEditor.Repaint();
        }
    }

    float GetNormalizedTime()
    {
        return NormalizeTime(mCurrentTime);
    }

    float NormalizeTime(float time)
    {
        return (time - mStartTime) / (mStopTime - mStartTime);
    }

    float UnnormalizeTime(float time)
    {
        return time * (mStopTime - mStartTime) + mStartTime;
    }

    void AddEvent()
    {
        var newEvent = new AnimationEvent();
        newEvent.time = GetNormalizedTime();
        ArrayUtility.Add(ref mEvents, newEvent);
        ApplyChanges();
        GUIUtility.ExitGUI();
    }

    void RemoveSeletedEvents()
    {
        mSelectedEvents.Sort();

        for (int i = mSelectedEvents.Count - 1; i >= 0; --i)
        {
            ArrayUtility.RemoveAt(ref mEvents, mSelectedEvents[i]);
        }

        mSelectedEvents.Clear();
        ApplyChanges();
        GUIUtility.ExitGUI();
    }

    void CopyAnimationEvents()
    {
        SerializedAnimationEventsJSON events = new SerializedAnimationEventsJSON();

        foreach (var index in mSelectedEvents)
        {
            ArrayUtility.Add(ref events.Events, new SerializedAnimationEvent(mEvents[index]));
        }

        EditorGUIUtility.systemCopyBuffer = JsonUtility.ToJson(events, true);
    }

    void PasteAnimationEvents()
    {
        mSelectedEvents.Clear();

        for (int i = 0; i < mCopiedAnimationEvents.Length; ++i)
        {
            mSelectedEvents.Add(mEvents.Length);
            ArrayUtility.Add(ref mEvents, mCopiedAnimationEvents[i]);
        }

        ApplyChanges();
        GUIUtility.ExitGUI();
    }

    void Revert()
    {
        if (mResetHashMethod != null)
        {
            mResetHashMethod.Invoke(mRootEditor, null);
        }

        mResetValuesMethod.Invoke(mRootEditor, null);
        GUIUtility.ExitGUI();
    }

    void ApplyAndImport()
    {
        // Workaround to avoid "ArgumentException: GUILayout: Mismatched LayoutGroup.repaint" from calling ApplyAndImport
        var oldTab = mActiveTabProperty.GetValue(mRootEditor);
        var newTab = (mTabsField.GetValue(mRootEditor) as object[])[0];
        mActiveTabProperty.SetValue(mRootEditor, newTab);

        mApplyAndImportMethod.Invoke(mRootEditor, null);

        mRootEditor.Repaint();

        mPostImport = () =>
        {
            mActiveTabProperty.SetValue(mRootEditor, oldTab);
            mRootEditor.Repaint();
        };
    }

    void ToolbarGUI()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.ExpandWidth(true));
        {
            if (GUILayout.Button("Add", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
            {
                AddEvent();
            }

            GUI.enabled = 0 < mSelectedEvents.Count;

            if (GUILayout.Button("Remove", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
            {
                RemoveSeletedEvents();
            }

            GUI.enabled = 0 < mSelectedEvents.Count;

            if (GUILayout.Button("Copy", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
            {
                CopyAnimationEvents();
            }

            if (Event.current.type == EventType.Layout)
            {
                mCopiedAnimationEvents = GetCopiedAnimatioEvents();
            }

            GUI.enabled = mCopiedAnimationEvents != null && 0 < mCopiedAnimationEvents.Length;

            if (GUILayout.Button("Paste", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
            {
                PasteAnimationEvents();
            }

            GUILayout.FlexibleSpace();

            GUI.enabled = mRootEditor.HasModified();

            if (GUILayout.Button("Revert", GUI.enabled ? Styles.ApplyRevertButton : EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
            {
                Revert();
            }

            if (GUILayout.Button("Apply", GUI.enabled ? Styles.ApplyRevertButton : EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
            {
                ApplyAndImport();
            }

            GUI.enabled = true;
        }
        GUILayout.EndHorizontal();
    }

    void OnClipSelect()
    {
        mClip = mClipField.GetValue(mAnimationClipEditor) as AnimationClip;
        mEvents = GetEvents(mClipInfo);
        mRootEditor = GetRootEditor();
        mSelectedEvents.Clear();
        mScroll = default;
    }

    bool GetTime(out float start, out float stop, out float current)
    {
        var avatarField = mAvatarPreviewField.GetValue(mAnimationClipEditor);
        if (avatarField != null)
        {
            var timeControl = mTimeControlField.GetValue(avatarField);
            if (timeControl != null)
            {
                start = (float)mStartTimeField.GetValue(timeControl);
                stop = (float)mStopTimeField.GetValue(timeControl);
                current = (float)mCurrentTimeField.GetValue(timeControl);
                return true;
            }
        }

        start = 0;
        stop = 0;
        current = 0;
        return false;
    }

    void EventGUI(int index)
    {
        ref AnimationEvent evt = ref mEvents[index];

        GUI.color = mSelectedEvents.Contains(index) ? new Color(0.5f, 0.75f, 1) : Color.white;

        GUILayout.BeginVertical(GUI.skin.box);
        {
            EditorGUILayout.Space();

            GUI.color = Color.white;

            EditorGUIUtility.labelWidth = 80;

            GUILayout.BeginHorizontal();
            {
                evt.time = NormalizeTime(EditorGUILayout.FloatField("Time", UnnormalizeTime(Mathf.Clamp01(evt.time))));
                if (GUILayout.Button("SET", EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    evt.time = GetNormalizedTime();
                    GUI.changed = true;
                }
            }
            GUILayout.EndHorizontal();

            evt.functionName = EditorGUILayout.TextField("Function", evt.functionName);
            evt.floatParameter = EditorGUILayout.FloatField("Float", evt.floatParameter);
            evt.intParameter = EditorGUILayout.IntField("Int", evt.intParameter);
            evt.stringParameter = EditorGUILayout.TextField("String", evt.stringParameter);
            evt.objectReferenceParameter = EditorGUILayout.ObjectField("Object", evt.objectReferenceParameter, typeof(Object), false);

            EditorGUILayout.Space();
        }
        GUILayout.EndVertical();

        Rect lastRect = GUILayoutUtility.GetLastRect();

        if (Event.current.type == EventType.MouseDown && lastRect.Contains(Event.current.mousePosition))
        {
            GUI.FocusControl(null);

            if (Event.current.control)
            {
                if (mSelectedEvents.Contains(index))
                {
                    mSelectedEvents.Remove(index);
                }
                else
                {
                    mSelectedEvents.Add(index);
                }
            }
            else if (Event.current.shift && 0 < mSelectedEvents.Count)
            {
                int start = mSelectedEvents[0];
                if (index != start)
                {
                    mSelectedEvents.Clear();

                    for (int i = start; i != index; i = i < index ? i + 1 : i - 1)
                    {
                        mSelectedEvents.Add(i);
                    }

                    mSelectedEvents.Add(index);
                }
            }
            else
            {
                mSelectedEvents.Clear();
                mSelectedEvents.Add(index);
            }

            Event.current.Use();
            Repaint();
        }
    }

}
