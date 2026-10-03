namespace ServerManager.Application.Docker;

public enum DockerContainerAction
{
    Start = 1,
    Stop = 2,
    Restart = 3,
    Pause = 4,
    Unpause = 5,
    Kill = 6,
    Remove = 7
}
