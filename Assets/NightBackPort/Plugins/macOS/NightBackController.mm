#import <Foundation/Foundation.h>
#import <GameController/GameController.h>
#import <CoreHaptics/CoreHaptics.h>

// Local GameController framework integration. Does not change device permissions/settings.
static GCController *active;
static CHHapticEngine *haptic;
static id<CHHapticPatternPlayer> player;
static float lastResistance=-1;
extern "C" int NB_Read(float *v, int count) {
    @autoreleasepool {
        if(count<22) return 0;
        memset(v,0,sizeof(float)*count);
        GCController *c=GCController.controllers.firstObject;
        if(c!=active){ active=c; haptic=nil; player=nil;lastResistance=-1; }
        GCExtendedGamepad *g=c.extendedGamepad;
        if(!g) return 0;
        v[0]=g.leftThumbstick.xAxis.value;v[1]=g.leftThumbstick.yAxis.value;
        v[2]=g.rightThumbstick.xAxis.value;v[3]=g.rightThumbstick.yAxis.value;
        v[4]=g.leftTrigger.value;v[5]=g.rightTrigger.value;
        v[6]=g.buttonA.pressed;v[7]=g.buttonB.pressed;v[8]=g.buttonX.pressed;v[9]=g.buttonY.pressed;
        v[10]=g.leftShoulder.pressed;v[11]=g.rightShoulder.pressed;
        v[12]=g.buttonMenu.pressed;v[13]=g.buttonOptions.pressed;
        v[14]=g.leftThumbstickButton.pressed;v[15]=g.rightThumbstickButton.pressed;
        v[16]=g.dpad.xAxis.value;v[17]=g.dpad.yAxis.value;
        v[18]=[g isKindOfClass:GCDualSenseGamepad.class];
        return 1;
    }
}
extern "C" void NB_Resistance(float strength) {
    @autoreleasepool {
        if(@available(macOS 12.0,*)) {
            if(![active.extendedGamepad isKindOfClass:GCDualSenseGamepad.class])return;
            strength=fmaxf(0,fminf(1,strength));
            if(fabsf(strength-lastResistance)<.025f)return;
            lastResistance=strength;
            GCDualSenseGamepad *g=(GCDualSenseGamepad*)active.extendedGamepad;
            if(strength>.01f)[g.rightTrigger setModeFeedbackWithStartPosition:.16f resistiveStrength:strength];
            else {[g.rightTrigger setModeOff];[g.leftTrigger setModeOff];}
        }
    }
}
extern "C" void NB_Pulse(float intensity,float sharpness,float duration) {
    @autoreleasepool {
        if(!active.haptics)return;
        NSError *error=nil;
        if(!haptic){haptic=[active.haptics createEngineWithLocality:GCHapticsLocalityDefault];[haptic startAndReturnError:&error];}
        if(!haptic||error)return;
        CHHapticEventParameter *a=[[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticIntensity value:fminf(1,intensity)];
        CHHapticEventParameter *b=[[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticSharpness value:fminf(1,sharpness)];
        CHHapticEvent *e=[[CHHapticEvent alloc] initWithEventType:CHHapticEventTypeHapticContinuous parameters:@[a,b] relativeTime:0 duration:fminf(.6f,duration)];
        CHHapticPattern *pattern=[[CHHapticPattern alloc] initWithEvents:@[e] parameters:@[] error:&error];
        if(error)return;
        [player stopAtTime:0 error:nil];player=[haptic createPlayerWithPattern:pattern error:&error];
        if(!error)[player startAtTime:0 error:&error];
    }
}
extern "C" void NB_Stop() {
    NB_Resistance(0);
    @autoreleasepool {[player stopAtTime:0 error:nil];player=nil;}
}
