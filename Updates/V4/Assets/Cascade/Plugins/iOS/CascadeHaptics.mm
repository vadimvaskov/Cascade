// Тактильная отдача для iOS: мягкий отклик на постановку сферы и отклик ошибки.
#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator *cascadeLightGenerator = nil;
static UIImpactFeedbackGenerator *cascadeRigidGenerator = nil;

extern "C" {

void _CascadeHapticLight()
{
    if (cascadeLightGenerator == nil) {
        cascadeLightGenerator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
    }
    [cascadeLightGenerator impactOccurred];
    [cascadeLightGenerator prepare];
}

void _CascadeHapticError()
{
    if (cascadeRigidGenerator == nil) {
        cascadeRigidGenerator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleRigid];
    }
    [cascadeRigidGenerator impactOccurred];
    [cascadeRigidGenerator prepare];
}

}
