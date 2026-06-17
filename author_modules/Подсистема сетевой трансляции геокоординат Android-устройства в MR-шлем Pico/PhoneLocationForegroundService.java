package com.phonepicoprovider.locationbridge;

import android.Manifest;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.content.pm.ServiceInfo;
import android.location.Location;
import android.location.LocationListener;
import android.location.LocationManager;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.HandlerThread;
import android.os.IBinder;
import android.os.Looper;
import android.os.PowerManager;
import android.provider.Settings;
import android.util.Log;

import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetAddress;
import java.nio.charset.StandardCharsets;
import java.util.Locale;

public final class PhoneLocationForegroundService extends Service implements LocationListener {
    public static final String ACTION_START = "com.phonepicoprovider.locationbridge.START";
    public static final String ACTION_STOP = "com.phonepicoprovider.locationbridge.STOP";
    public static final String EXTRA_TARGET_ADDRESS = "targetAddress";
    public static final String EXTRA_TARGET_PORT = "targetPort";
    public static final String EXTRA_SEND_INTERVAL_SECONDS = "sendIntervalSeconds";
    public static final String EXTRA_UPDATE_DISTANCE_METERS = "updateDistanceMeters";
    public static final String EXTRA_DEVICE_ID = "deviceId";
    public static final String EXTRA_ALLOW_BROADCAST = "allowBroadcast";
    public static final String EXTRA_KEEP_CPU_AWAKE = "keepCpuAwake";

    private static final String TAG = "PhoneLocationService";
    private static final String CHANNEL_ID = "phone_location_bridge";
    private static final int NOTIFICATION_ID = 47777;

    private final Object locationLock = new Object();
    private final Runnable sendRunnable = new Runnable() {
        @Override
        public void run() {
            sendLatestLocation();
            scheduleNextSend();
        }
    };

    private LocationManager locationManager;
    private HandlerThread workerThread;
    private Handler workerHandler;
    private PowerManager.WakeLock wakeLock;
    private DatagramSocket socket;
    private InetAddress targetInetAddress;
    private Location lastLocation;
    private Location previousLocation;
    private String targetAddress = "255.255.255.255";
    private int targetPort = 47777;
    private long sendIntervalMillis = 1000L;
    private float updateDistanceMeters = 1f;
    private String deviceId;
    private boolean allowBroadcast = true;
    private boolean keepCpuAwake = true;
    private int sequence;

    @Override
    public void onCreate() {
        super.onCreate();
        locationManager = (LocationManager)getSystemService(Context.LOCATION_SERVICE);
        workerThread = new HandlerThread("PhoneLocationUdpSender");
        workerThread.start();
        workerHandler = new Handler(workerThread.getLooper());
        deviceId = Settings.Secure.getString(getContentResolver(), Settings.Secure.ANDROID_ID);
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent != null && ACTION_STOP.equals(intent.getAction())) {
            stopSelf();
            return START_NOT_STICKY;
        }

        readConfig(intent);
        startAsForegroundService();
        acquireWakeLockIfNeeded();
        openSocket();
        startLocationUpdates();
        scheduleNextSend();
        return START_STICKY;
    }

    @Override
    public void onDestroy() {
        stopLocationUpdates();
        releaseWakeLock();
        closeSocket();

        if (workerHandler != null) {
            workerHandler.removeCallbacksAndMessages(null);
        }

        if (workerThread != null) {
            workerThread.quitSafely();
            workerThread = null;
            workerHandler = null;
        }

        super.onDestroy();
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public void onLocationChanged(Location location) {
        synchronized (locationLock) {
            previousLocation = lastLocation;
            lastLocation = new Location(location);
            sequence++;
        }
    }

    @Override
    public void onStatusChanged(String provider, int status, Bundle extras) {
    }

    @Override
    public void onProviderEnabled(String provider) {
        startLocationUpdates();
    }

    @Override
    public void onProviderDisabled(String provider) {
    }

    private void readConfig(Intent intent) {
        if (intent == null) {
            return;
        }

        targetAddress = intent.getStringExtra(EXTRA_TARGET_ADDRESS);
        if (targetAddress == null || targetAddress.length() == 0) {
            targetAddress = "255.255.255.255";
        }

        targetPort = clamp(intent.getIntExtra(EXTRA_TARGET_PORT, 47777), 1, 65535);
        float intervalSeconds = Math.max(0.05f, intent.getFloatExtra(EXTRA_SEND_INTERVAL_SECONDS, 1f));
        sendIntervalMillis = Math.max(50L, (long)(intervalSeconds * 1000f));
        updateDistanceMeters = Math.max(0f, intent.getFloatExtra(EXTRA_UPDATE_DISTANCE_METERS, 1f));
        allowBroadcast = intent.getBooleanExtra(EXTRA_ALLOW_BROADCAST, true);
        keepCpuAwake = intent.getBooleanExtra(EXTRA_KEEP_CPU_AWAKE, true);

        String requestedDeviceId = intent.getStringExtra(EXTRA_DEVICE_ID);
        if (requestedDeviceId != null && requestedDeviceId.length() > 0) {
            deviceId = requestedDeviceId;
        }
    }

    private void startAsForegroundService() {
        createNotificationChannel();
        Notification notification = buildNotification();

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(NOTIFICATION_ID, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_LOCATION);
        } else {
            startForeground(NOTIFICATION_ID, notification);
        }
    }

    private Notification buildNotification() {
        Intent launchIntent = getPackageManager().getLaunchIntentForPackage(getPackageName());
        PendingIntent contentIntent = null;
        if (launchIntent != null) {
            contentIntent = PendingIntent.getActivity(
                    this,
                    0,
                    launchIntent,
                    pendingIntentFlags(PendingIntent.FLAG_UPDATE_CURRENT));
        }

        Intent stopIntent = new Intent(this, PhoneLocationForegroundService.class);
        stopIntent.setAction(ACTION_STOP);
        PendingIntent stopPendingIntent = PendingIntent.getService(
                this,
                1,
                stopIntent,
                pendingIntentFlags(PendingIntent.FLAG_UPDATE_CURRENT));

        Notification.Builder builder = Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                ? new Notification.Builder(this, CHANNEL_ID)
                : new Notification.Builder(this);

        builder.setSmallIcon(android.R.drawable.ic_menu_mylocation)
                .setContentTitle("Phone location bridge")
                .setContentText("Sending phone GPS to Pico")
                .setOngoing(true)
                .setShowWhen(false)
                .addAction(android.R.drawable.ic_menu_close_clear_cancel, "Stop", stopPendingIntent);

        if (contentIntent != null) {
            builder.setContentIntent(contentIntent);
        }

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            builder.setCategory(Notification.CATEGORY_SERVICE);
        }

        return builder.build();
    }

    private void createNotificationChannel() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) {
            return;
        }

        NotificationManager manager = (NotificationManager)getSystemService(Context.NOTIFICATION_SERVICE);
        if (manager == null || manager.getNotificationChannel(CHANNEL_ID) != null) {
            return;
        }

        NotificationChannel channel = new NotificationChannel(
                CHANNEL_ID,
                "Phone location bridge",
                NotificationManager.IMPORTANCE_LOW);
        channel.setDescription("Keeps phone GPS broadcasting while the app is in the background.");
        manager.createNotificationChannel(channel);
    }

    private void acquireWakeLockIfNeeded() {
        if (!keepCpuAwake || wakeLock != null) {
            return;
        }

        PowerManager powerManager = (PowerManager)getSystemService(Context.POWER_SERVICE);
        if (powerManager == null) {
            return;
        }

        wakeLock = powerManager.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "phonepicoprovider:LocationBridge");
        wakeLock.setReferenceCounted(false);
        wakeLock.acquire();
    }

    private void releaseWakeLock() {
        if (wakeLock != null && wakeLock.isHeld()) {
            wakeLock.release();
        }

        wakeLock = null;
    }

    private void openSocket() {
        closeSocket();

        try {
            targetInetAddress = InetAddress.getByName(targetAddress);
            socket = new DatagramSocket();
            socket.setBroadcast(allowBroadcast);
        } catch (Exception exception) {
            Log.w(TAG, "Failed to open UDP socket: " + exception.getMessage());
            closeSocket();
        }
    }

    private void closeSocket() {
        if (socket != null) {
            socket.close();
            socket = null;
        }

        targetInetAddress = null;
    }

    private void startLocationUpdates() {
        if (!hasLocationPermission() || locationManager == null) {
            Log.w(TAG, "Location permission is missing or LocationManager is unavailable.");
            return;
        }

        stopLocationUpdates();
        requestProvider(LocationManager.GPS_PROVIDER);
        requestProvider(LocationManager.NETWORK_PROVIDER);
    }

    private void requestProvider(String provider) {
        try {
            if (!locationManager.isProviderEnabled(provider)) {
                return;
            }

            locationManager.requestLocationUpdates(
                    provider,
                    Math.max(250L, sendIntervalMillis / 2L),
                    updateDistanceMeters,
                    this,
                    Looper.getMainLooper());

            Location knownLocation = locationManager.getLastKnownLocation(provider);
            if (knownLocation != null) {
                onLocationChanged(knownLocation);
            }
        } catch (SecurityException exception) {
            Log.w(TAG, "Location permission denied for provider " + provider + ".");
        } catch (Exception exception) {
            Log.w(TAG, "Failed to request " + provider + " updates: " + exception.getMessage());
        }
    }

    private void stopLocationUpdates() {
        if (locationManager == null) {
            return;
        }

        try {
            locationManager.removeUpdates(this);
        } catch (SecurityException ignored) {
        }
    }

    private boolean hasLocationPermission() {
        return checkSelfPermissionCompat(Manifest.permission.ACCESS_FINE_LOCATION) ||
                checkSelfPermissionCompat(Manifest.permission.ACCESS_COARSE_LOCATION);
    }

    private boolean checkSelfPermissionCompat(String permission) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.M) {
            return true;
        }

        return checkSelfPermission(permission) == PackageManager.PERMISSION_GRANTED;
    }

    private void scheduleNextSend() {
        if (workerHandler == null) {
            return;
        }

        workerHandler.removeCallbacks(sendRunnable);
        workerHandler.postDelayed(sendRunnable, sendIntervalMillis);
    }

    private void sendLatestLocation() {
        Location location;
        Location previous;
        int currentSequence;

        synchronized (locationLock) {
            if (lastLocation == null) {
                return;
            }

            location = new Location(lastLocation);
            previous = previousLocation == null ? null : new Location(previousLocation);
            currentSequence = sequence;
        }

        if (socket == null || targetInetAddress == null) {
            openSocket();
        }

        if (socket == null || targetInetAddress == null) {
            return;
        }

        try {
            String json = buildJson(location, previous, currentSequence);
            byte[] bytes = json.getBytes(StandardCharsets.UTF_8);
            DatagramPacket packet = new DatagramPacket(bytes, bytes.length, targetInetAddress, targetPort);
            socket.send(packet);
        } catch (Exception exception) {
            Log.w(TAG, "Failed to send UDP location packet: " + exception.getMessage());
        }
    }

    private String buildJson(Location location, Location previous, int currentSequence) {
        double latitude = finite(location.getLatitude(), 0d);
        double longitude = finite(location.getLongitude(), 0d);
        double altitude = location.hasAltitude() ? finite(location.getAltitude(), 0d) : 0d;
        float horizontalAccuracy = location.hasAccuracy() ? finite(location.getAccuracy(), -1f) : -1f;
        float verticalAccuracy = -1f;

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O && location.hasVerticalAccuracy()) {
            verticalAccuracy = finite(location.getVerticalAccuracyMeters(), -1f);
        }

        float speed = location.hasSpeed() ? finite(location.getSpeed(), -1f) : estimateSpeed(location, previous);
        float course = location.hasBearing() ? finite(location.getBearing(), -1f) : estimateCourse(location, previous);
        double timestamp = location.getTime() > 0L
                ? (double)location.getTime() / 1000d
                : (double)System.currentTimeMillis() / 1000d;

        return String.format(
                Locale.US,
                "{\"MessageType\":\"phone_location\",\"ProtocolVersion\":1,\"Sample\":{\"DeviceId\":\"%s\",\"Latitude\":%.8f,\"Longitude\":%.8f,\"AltitudeMeters\":%.3f,\"HorizontalAccuracyMeters\":%.3f,\"VerticalAccuracyMeters\":%.3f,\"SpeedMetersPerSecond\":%.3f,\"CourseDegrees\":%.3f,\"Timestamp\":%.3f,\"Sequence\":%d}}",
                escapeJson(deviceId == null ? "" : deviceId),
                latitude,
                longitude,
                altitude,
                horizontalAccuracy,
                verticalAccuracy,
                speed,
                course,
                timestamp,
                currentSequence);
    }

    private static float estimateSpeed(Location location, Location previous) {
        if (previous == null) {
            return -1f;
        }

        long deltaMillis = location.getTime() - previous.getTime();
        if (deltaMillis <= 0L) {
            return -1f;
        }

        return location.distanceTo(previous) / ((float)deltaMillis / 1000f);
    }

    private static float estimateCourse(Location location, Location previous) {
        if (previous == null) {
            return -1f;
        }

        float distance = previous.distanceTo(location);
        if (distance <= 0.05f) {
            return -1f;
        }

        float bearing = previous.bearingTo(location);
        return bearing < 0f ? bearing + 360f : bearing;
    }

    private static int pendingIntentFlags(int flags) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            return flags | PendingIntent.FLAG_IMMUTABLE;
        }

        return flags;
    }

    private static int clamp(int value, int min, int max) {
        return Math.max(min, Math.min(max, value));
    }

    private static float finite(float value, float fallback) {
        return Float.isNaN(value) || Float.isInfinite(value) ? fallback : value;
    }

    private static double finite(double value, double fallback) {
        return Double.isNaN(value) || Double.isInfinite(value) ? fallback : value;
    }

    private static String escapeJson(String value) {
        return value.replace("\\", "\\\\").replace("\"", "\\\"");
    }
}
