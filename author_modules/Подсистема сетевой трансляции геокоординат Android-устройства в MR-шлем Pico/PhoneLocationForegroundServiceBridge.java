package com.phonepicoprovider.locationbridge;

import android.content.Context;
import android.content.Intent;
import android.os.Build;

public final class PhoneLocationForegroundServiceBridge {
    private PhoneLocationForegroundServiceBridge() {
    }

    public static void start(
            Context context,
            String targetAddress,
            int targetPort,
            float sendIntervalSeconds,
            float updateDistanceMeters,
            String deviceId,
            boolean allowBroadcast,
            boolean keepCpuAwake) {
        Context appContext = context.getApplicationContext();
        Intent intent = new Intent(appContext, PhoneLocationForegroundService.class);
        intent.setAction(PhoneLocationForegroundService.ACTION_START);
        intent.putExtra(PhoneLocationForegroundService.EXTRA_TARGET_ADDRESS, targetAddress);
        intent.putExtra(PhoneLocationForegroundService.EXTRA_TARGET_PORT, targetPort);
        intent.putExtra(PhoneLocationForegroundService.EXTRA_SEND_INTERVAL_SECONDS, sendIntervalSeconds);
        intent.putExtra(PhoneLocationForegroundService.EXTRA_UPDATE_DISTANCE_METERS, updateDistanceMeters);
        intent.putExtra(PhoneLocationForegroundService.EXTRA_DEVICE_ID, deviceId);
        intent.putExtra(PhoneLocationForegroundService.EXTRA_ALLOW_BROADCAST, allowBroadcast);
        intent.putExtra(PhoneLocationForegroundService.EXTRA_KEEP_CPU_AWAKE, keepCpuAwake);

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            appContext.startForegroundService(intent);
        } else {
            appContext.startService(intent);
        }
    }

    public static void stop(Context context) {
        Context appContext = context.getApplicationContext();
        Intent intent = new Intent(appContext, PhoneLocationForegroundService.class);
        appContext.stopService(intent);
    }
}
