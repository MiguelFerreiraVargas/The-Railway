using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

public class TrainAnimationMixer : MonoBehaviour
{
    public AnimationClip[] animations;

    private PlayableGraph graph;

    void Start()
    {
        Animator animator = GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogError("O Steam Train precisa ter um Animator!");
            return;
        }

        if (animations == null || animations.Length == 0)
        {
            Debug.LogError("Nenhuma animação foi colocada no Train Animation Mixer!");
            return;
        }

        graph = PlayableGraph.Create("Train Animation Mixer");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        AnimationPlayableOutput output =
            AnimationPlayableOutput.Create(graph, "Train", animator);

        AnimationMixerPlayable mixer =
            AnimationMixerPlayable.Create(graph, animations.Length);

        output.SetSourcePlayable(mixer);

        for (int i = 0; i < animations.Length; i++)
        {
            if (animations[i] == null)
                continue;

            AnimationClipPlayable clip =
                AnimationClipPlayable.Create(graph, animations[i]);

            clip.SetDuration(animations[i].length);

            graph.Connect(clip, 0, mixer, i);
            mixer.SetInputWeight(i, 1f);
        }

        graph.Play();
    }

    void OnDestroy()
    {
        if (graph.IsValid())
            graph.Destroy();
    }
}