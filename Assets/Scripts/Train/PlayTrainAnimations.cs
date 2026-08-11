using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(TrainAnimationMixer))]
public class TrainAnimationMixerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);

        TrainAnimationMixer train =
            (TrainAnimationMixer)target;

        if (GUILayout.Button("CARREGAR TODAS AS ANIMAÇÕES DO TREM"))
        {
            string[] guids = AssetDatabase.FindAssets(
                "steam-train t:Model"
            );

            if (guids.Length == 0)
            {
                Debug.LogError(
                    "Não encontrei o steam-train.fbx dentro da pasta Assets."
                );
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);

            Object[] assets =
                AssetDatabase.LoadAllAssetsAtPath(path);

            AnimationClip[] clips = assets
                .OfType<AnimationClip>()
                .ToArray();

            if (clips.Length == 0)
            {
                Debug.LogError(
                    "Nenhuma Animation Clip encontrada no steam-train.fbx."
                );
                return;
            }

            train.animations = clips;

            EditorUtility.SetDirty(train);

            Debug.Log(
                "Carregadas " + clips.Length +
                " animações do trem!"
            );
        }
    }
}