﻿using ToonRP.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ToonRP.Editor
{
    [CustomPropertyDrawer(typeof(ToonRampSettings))]
    public class ToonRampSettingsPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();

            root.Add(new ToonRpHeaderLabel("Global Ramp"));

            SerializedProperty crispAntiAliasedProperty =
                property.FindPropertyRelative(nameof(ToonRampSettings.CrispAntiAliased));
            var crispAntiAliasedField =
                new PropertyField(crispAntiAliasedProperty);
            var smoothnessField =
                new PropertyField(property.FindPropertyRelative(nameof(ToonRampSettings.Smoothness)));
            var specularSmoothnessField =
                new PropertyField(property.FindPropertyRelative(nameof(ToonRampSettings.SpecularSmoothness)));

            void RefreshSmoothnessFields()
            {
                bool showSmoothness = !crispAntiAliasedProperty.boolValue;
                smoothnessField.SetEnabled(showSmoothness);
                specularSmoothnessField.SetEnabled(showSmoothness);
            }

            RefreshSmoothnessFields();

            crispAntiAliasedField.RegisterValueChangeCallback(_ => RefreshSmoothnessFields());

            root.Add(new PropertyField(property.FindPropertyRelative(nameof(ToonRampSettings.Threshold))));
            root.Add(new PropertyField(property.FindPropertyRelative(nameof(ToonRampSettings.SpecularThreshold))));
            root.Add(crispAntiAliasedField);
            root.Add(smoothnessField);
            root.Add(specularSmoothnessField);
            root.Add(new PropertyField(property.FindPropertyRelative(nameof(ToonRampSettings.ShadowColor))));

            return root;
        }
    }
}