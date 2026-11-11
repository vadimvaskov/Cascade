#import <UIKit/UIKit.h>

extern "C" void CascadeLightImpact()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        static UIImpactFeedbackGenerator *generator = nil;
        if (!generator) generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
        [generator impactOccurredWithIntensity:0.4];
        [generator prepare];
    });
}
