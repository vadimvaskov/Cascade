#import <AVFoundation/AVFoundation.h>
#include <cmath>

// Read the active route without changing Unity's session, sample rate or buffer settings.
extern "C" int CascadeReadAudioOutputTiming(double *outputLatency, double *ioBufferDuration)
{
    if (!outputLatency || !ioBufferDuration) return 0;
    AVAudioSession *session = [AVAudioSession sharedInstance];
    const double output = session.outputLatency;
    const double io = session.IOBufferDuration;
    if (!std::isfinite(output) || !std::isfinite(io) || output < 0 || io <= 0) return 0;
    *outputLatency = output;
    *ioBufferDuration = io;
    return 1;
}
