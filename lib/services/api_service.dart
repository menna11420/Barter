import 'dart:async';
import 'package:dio/dio.dart';
import 'package:google_sign_in/google_sign_in.dart';
import 'package:barter/model/chat_model.dart';
import 'package:barter/model/exchange_model.dart';
import 'package:barter/model/item_model.dart';
import 'package:barter/model/user_model.dart';
import 'package:barter/model/notification_model.dart';
import 'package:barter/model/review_model.dart';
import 'package:flutter/foundation.dart';
import 'api_client.dart';

class UserMetadata {
  final DateTime? creationTime;
  UserMetadata({this.creationTime});
}

class User {
  final String uid;
  final String? email;
  String? displayName;
  String? photoURL;
  bool isAnonymous;
  bool emailVerified;
  UserMetadata metadata;
  
  User({
    required this.uid, 
    this.email, 
    this.displayName,
    this.photoURL,
    this.isAnonymous = false,
    this.emailVerified = true,
    UserMetadata? metadata,
  }) : metadata = metadata ?? UserMetadata(creationTime: DateTime.now());

  Future<void> updateDisplayName(String name) async {
    displayName = name;
  }
  
  Future<void> updatePhotoURL(String? url) async {
    photoURL = url;
  }
  
  Future<void> sendEmailVerification() async {
    await ApiService.sendEmailVerification();
  }
}

class UserCredential {
  final User? user;
  UserCredential({this.user});
}

class ApiService {
  static final ApiClient _client = ApiClient();
  static final GoogleSignIn _googleSignIn = GoogleSignIn();
  
  static User? _currentUser;
  static User? get currentUser => _currentUser;
  static bool get isEmailVerified => true; // Assume verified

  // Global notifier to trigger UI refreshes when items are modified
  static final ValueNotifier<int> itemsNotifier = ValueNotifier<int>(0);

  // ==================== CACHE ====================
  static void clearCache() {}
  static void initializeFirestore() {}
  static Future<void> ensureUserDocument() async {}
  static Future<void> reloadUser() async {}

  static Future<void> tryRestoreSession() async {
    final token = await _client.secureStorage.read(key: 'jwt_token');
    final isGuest = await _client.secureStorage.read(key: 'is_guest') == 'true';

    if (token != null && token.isNotEmpty) {
      try {
        final res = await _client.dio.get('/auth/me');
        final userDto = res.data;
        _currentUser = User(
          uid: userDto['id'],
          email: userDto['email'],
          displayName: userDto['name'],
          photoURL: userDto['photoUrl'],
          emailVerified: userDto['emailVerified'] ?? true,
        );
      } catch (e) {
        print('Session restoration failed: $e');
        await _client.clearToken();
      }
    } else if (isGuest) {
      final guestUid = await _client.secureStorage.read(key: 'guest_uid');
      final guestName = await _client.secureStorage.read(key: 'guest_name');
      _currentUser = User(
        uid: guestUid ?? 'guest',
        displayName: guestName ?? 'Guest',
        isAnonymous: true,
      );
    }
  }

  // ==================== AUTH ====================
  static Future<UserCredential> signUp(String email, String password, String name) async {
    try {
      final res = await _client.dio.post('/auth/register', data: {
        'name': name,
        'email': email,
        'password': password,
      });
      await _client.saveToken(res.data['token']);
      final userDto = res.data['user'];
      _currentUser = User(uid: userDto['id'], email: userDto['email'], displayName: userDto['name']);
      return UserCredential(user: _currentUser);
    } catch (e) {
      throw Exception('Sign up failed: $e');
    }
  }

  static Future<UserCredential> signIn(String email, String password) async {
    try {
      final res = await _client.dio.post('/auth/login', data: {
        'email': email,
        'password': password,
      });
      await _client.saveToken(res.data['token']);
      final userDto = res.data['user'];
      _currentUser = User(uid: userDto['id'], email: userDto['email'], displayName: userDto['name']);
      return UserCredential(user: _currentUser);
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw Exception('invalid-credential');
      throw Exception('SignIn failed: $e');
    }
  }

  static Future<void> logout() async {
    await _client.clearToken();
    await _client.secureStorage.delete(key: 'is_guest');
    await _client.secureStorage.delete(key: 'guest_uid');
    await _client.secureStorage.delete(key: 'guest_name');
    _currentUser = null;
  }

  static Future<UserCredential?> signInWithGoogle() async {
    try {
      final GoogleSignInAccount? googleUser = await _googleSignIn.signIn();
      if (googleUser == null) return null;

      final GoogleSignInAuthentication googleAuth = await googleUser.authentication;
      if (googleAuth.idToken == null) throw Exception('No ID Token');

      final res = await _client.dio.post('/auth/google', data: {
        'idToken': googleAuth.idToken,
      });

      await _client.saveToken(res.data['token']);
      final userDto = res.data['user'];
      _currentUser = User(uid: userDto['id'], email: userDto['email'], displayName: userDto['name']);
      return UserCredential(user: _currentUser);
    } catch (e) {
      print('Google sign in error: $e');
      return null;
    }
  }

  static Future<void> resetPassword(String email) async {
    await _client.dio.post('/auth/reset-password', data: {'email': email});
  }

  static Future<void> sendEmailVerification() async {
    // In our backend, OTP handles email verification
  }

  static Future<void> generateAndSendOtp(String uid) async {
    await _client.dio.post('/auth/send-otp', data: {'userId': uid});
  }

  static Future<bool> verifyOtp(String uid, String code) async {
    try {
      await _client.dio.post('/auth/verify-otp', data: {
        'userId': uid,
        'code': code,
      });
      return true;
    } catch (e) {
      return false;
    }
  }

  static Future<UserCredential> signInAnonymously() async {
    final guestUid = 'guest_${DateTime.now().millisecondsSinceEpoch}';
    const guestName = 'Guest';
    
    _currentUser = User(
      uid: guestUid,
      displayName: guestName,
      isAnonymous: true,
    );

    await _client.secureStorage.write(key: 'is_guest', value: 'true');
    await _client.secureStorage.write(key: 'guest_uid', value: guestUid);
    await _client.secureStorage.write(key: 'guest_name', value: guestName);

    return UserCredential(user: _currentUser);
  }

  // ==================== USERS ====================
  static Future<UserModel?> getUserById(String uid) async {
    try {
      final res = await _client.dio.get('/users/$uid');
      return UserModel.fromJson(_mapUserDto(res.data));
    } catch (e) {
      return null;
    }
  }

  static Future<void> updateUser(UserModel user) async {
    await _client.dio.put('/users/${user.uid}', data: {
      'name': user.name,
      'photoUrl': user.photoUrl,
      'phone': user.phone,
      'location': user.location,
    });
  }

  // Helper to map .NET DTO to the expected UserModel.fromJson map format.
  static Map<String, dynamic> _mapUserDto(Map<String, dynamic> json) {
    return {
      'uid': json['id'],
      'name': json['name'],
      'email': json['email'],
      'photoUrl': json['photoUrl'],
      'phone': json['phone'],
      'location': json['location'],
      'isEmailVerified': json['emailVerified'],
      'isMfaEnabled': json['mfaEnabled'],
      'averageRating': json['averageRating'],
      'createdAt': json['createdAt'],
    };
  }

  // ==================== ITEMS ====================
  static Future<String> addItemDirect(Map<String, dynamic> itemData) async {
    try {
      final data = {
        'title': itemData['title'],
        'description': itemData['description'],
        'imageUrls': itemData['imageUrls'] ?? [],
        'category': itemData['category'],
        'condition': itemData['condition'],
        'preferredExchange': itemData['preferredExchange'],
        'location': itemData['location'],
        'latitude': itemData['latitude'],
        'longitude': itemData['longitude'],
        'detailedAddress': itemData['detailedAddress'],
        'itemType': itemData['itemType'] is int ? itemData['itemType'] : (itemData['itemType'] == 'service' ? 1 : 0),
        'isRemote': itemData['isRemote'] ?? false,
        'isAvailable': itemData['isAvailable'] ?? true,
      };
      
      final res = await _client.dio.post('/items', data: data);
      itemsNotifier.value++;
      return res.data['id'].toString(); // Ensure it returns as String safely
    } catch (e, stack) {
      print('=== CRITICAL ERROR IN addItemDirect ===');
      print(e);
      print(stack);
      rethrow;
    }
  }

  static Future<void> updateItemDirect(String id, Map<String, dynamic> itemData) async {
    try {
      final data = {
        'title': itemData['title'],
        'description': itemData['description'],
        'imageUrls': itemData['imageUrls'] ?? [],
        'category': itemData['category'],
        'condition': itemData['condition'],
        'preferredExchange': itemData['preferredExchange'],
        'location': itemData['location'],
        'latitude': itemData['latitude'],
        'longitude': itemData['longitude'],
        'detailedAddress': itemData['detailedAddress'],
        'itemType': itemData['itemType'] is int ? itemData['itemType'] : (itemData['itemType'] == 'service' ? 1 : 0),
        'isRemote': itemData['isRemote'] ?? false,
        'isAvailable': itemData['isAvailable'],
      };
      await _client.dio.put('/items/$id', data: data);
      itemsNotifier.value++;
    } catch (e, stack) {
      print('=== CRITICAL ERROR IN updateItemDirect ===');
      print(e);
      print(stack);
      rethrow;
    }
  }

  static Future<void> updateItem(ItemModel item) async {
    await updateItemDirect(item.id, item.toJson());
  }

  static Future<void> deleteItem(String id) async {
    await _client.dio.delete('/items/$id');
    itemsNotifier.value++;
  }

  static Future<List<ItemModel>> getItems() async {
    final res = await _client.dio.get('/items');
    return (res.data as List).map((i) => ItemModel.fromJson(i as Map<String, dynamic>)).toList();
  }

  static Future<List<ItemModel>> getUserItems(String? userId) async {
    if (userId == null) return [];
    final res = await _client.dio.get('/items/user/$userId');
    return (res.data as List).map((i) => ItemModel.fromJson(i as Map<String, dynamic>)).toList();
  }



  static int _mapCategoryToInt(dynamic category) {
    if (category is int) return category;
    if (category == null) return 5; // Other
    switch(category.toString()) {
      case 'Electronics': return 0;
      case 'Clothing': return 1;
      case 'Books': return 2;
      case 'Furniture': return 3;
      case 'Sports': return 4;
      case 'Service': return 6;
      default: return 5;
    }
  }

  static String _mapCategoryFromInt(int cat) {
    switch(cat) {
      case 0: return 'Electronics';
      case 1: return 'Clothing';
      case 2: return 'Books';
      case 3: return 'Furniture';
      case 4: return 'Sports';
      case 6: return 'Service';
      default: return 'Other';
    }
  }

  static int _mapConditionToInt(dynamic condition) {
    if (condition is int) return condition;
    if (condition == null) return 2; // Good
    switch(condition.toString()) {
      case 'New': return 0;
      case 'Like New': return 1;
      case 'Good': return 2;
      case 'Fair': return 3;
      case 'Poor': return 4;
      default: return 2;
    }
  }

  static String _mapConditionFromInt(int cond) {
    switch(cond) {
      case 0: return 'New';
      case 1: return 'Like New';
      case 2: return 'Good';
      case 3: return 'Fair';
      case 4: return 'Poor';
      default: return 'Good';
    }
  }

  // ==================== SAVED ITEMS ====================
  static Future<bool> isItemSaved(String? userId, String itemId) async {
    if (userId == null) return false;
    final res = await _client.dio.get('/saved/$itemId/check');
    return res.data['isSaved'];
  }

  static Future<bool> toggleSavedItem(String? userId, String itemId) async {
    if (userId == null) return false;
    final res = await _client.dio.post('/saved/$itemId/toggle');
    return res.data['saved'];
  }
  
  static Stream<List<String>> getSavedItemsStream(String userId) {
    return Stream.fromFuture(_client.dio.get('/saved').then((res) {
      return List<String>.from(res.data);
    }));
  }

  // ==================== REVIEWS ====================
  static Stream<List<ReviewModel>> getUserReviews(String userId) {
    return Stream.fromFuture(_client.dio.get('/users/$userId/reviews').then((res) {
      return (res.data as List).map((r) => ReviewModel.fromJson({
        'id': r['id'],
        'reviewerId': r['reviewerId'],
        'revieweeId': r['revieweeId'],
        'exchangeId': r['exchangeId'],
        'rating': r['rating'],
        'comment': r['comment'],
        'createdAt': r['createdAt'],
      })).toList();
    }));
  }

  // ==================== EXCHANGES ====================
  static Future<void> createExchangeProposal(ExchangeModel exchange) async {
    await _client.dio.post('/exchanges', data: {
      'proposedTo': exchange.proposedTo,
      'itemsOffered': exchange.itemsOffered.map((i) => {'itemId': i.itemId, 'title': i.title, 'imageUrl': i.imageUrl}).toList(),
      'itemsRequested': exchange.itemsRequested.map((i) => {'itemId': i.itemId, 'title': i.title, 'imageUrl': i.imageUrl}).toList(),
      'notes': exchange.meetingLocation, // mapping to notes or meetingLocation
    });
  }

  static Future<List<ExchangeModel>> getItemExchanges(String itemId) async {
    // We would need a custom backend endpoint for this or fetch user exchanges and filter locally
    final res = await _client.dio.get('/exchanges');
    final all = (res.data as List).map((e) => ExchangeModel.fromJson(e)).toList();
    return all.where((e) => e.itemsOffered.any((i) => i.itemId == itemId) || 
                            e.itemsRequested.any((i) => i.itemId == itemId)).toList();
  }

  static Future<List<ExchangeModel>> getUserExchanges(String userId) async {
    final res = await _client.dio.get('/exchanges');
    return (res.data as List).map((e) => ExchangeModel.fromJson(e)).toList();
  }
  
  static Future<List<ExchangeModel>> getPendingExchanges([String? userId]) async {
    final uid = userId ?? _currentUser?.uid;
    if (uid == null) return [];
    
    final res = await _client.dio.get('/exchanges/pending');
    return (res.data as List).map((e) => ExchangeModel.fromJson(e)).toList();
  }

  static Future<void> acceptExchange(String exchangeId) async {
    await _client.dio.put('/exchanges/$exchangeId/accept');
  }

  static Future<void> cancelExchange(String exchangeId) async {
    await _client.dio.put('/exchanges/$exchangeId/cancel');
  }

  static Future<void> confirmExchangeCompletion(String exchangeId) async {
    await _client.dio.put('/exchanges/$exchangeId/confirm');
  }

  static Future<void> submitExchangeRating(String exchangeId, double rating, String review) async {
    await _client.dio.post('/exchanges/$exchangeId/rate', data: {
      'rating': rating,
      'review': review,
    });
  }
  
  static Future<void> updateExchangeMeeting(String exchangeId, String location, DateTime date) async {
    await _client.dio.put('/exchanges/$exchangeId/meeting', data: {
      'location': location,
      'date': date.toIso8601String(),
    });
  }

  // ==================== NOTIFICATIONS ====================
  static Future<List<NotificationModel>> getUserNotifications(String userId) async {
    final res = await _client.dio.get('/notifications');
    return (res.data as List).map((n) => NotificationModel.fromJson(n)).toList();
  }

  static Future<void> markNotificationAsRead(String id) async {
    await _client.dio.put('/notifications/$id/read');
  }

  static Future<void> markAllNotificationsAsRead(String userId) async {
    await _client.dio.put('/notifications/read-all');
  }

  // ==================== CHATS & MESSAGES ====================
  static Future<String> createOrGetChat(String currentUserId, String otherUserId, String itemId, [String itemTitle = '']) async {
    final res = await _client.dio.post('/chats', data: {
      'otherUserId': otherUserId,
      'itemId': itemId,
      'itemTitle': itemTitle,
    });
    return res.data['chatId'];
  }

  static Stream<List<ChatModel>> getUserChatsStream([String? userId]) async* {
    final uid = userId ?? _currentUser?.uid;
    if (uid == null) {
      yield [];
      return;
    }

    try {
      final res = await _client.dio.get('/chats');
      yield (res.data as List).map((c) => ChatModel.fromJson({
        'chatId': c['chatId'],
        'participants': c['participants'],
        'itemId': c['itemId'],
        'itemTitle': c['itemTitle'],
        'lastMessage': c['lastMessage'],
        'lastMessageTime': c['lastMessageTime'],
        'lastSenderId': c['lastSenderId'],
        'unreadCount': c['unreadCount'],
      })).toList();
    } catch (e) {
      print('Initial getUserChatsStream error: $e');
    }

    yield* Stream.periodic(const Duration(seconds: 3)).asyncMap((_) async {
      try {
        final res = await _client.dio.get('/chats');
        return (res.data as List).map((c) => ChatModel.fromJson({
          'chatId': c['chatId'],
          'participants': c['participants'],
          'itemId': c['itemId'],
          'itemTitle': c['itemTitle'],
          'lastMessage': c['lastMessage'],
          'lastMessageTime': c['lastMessageTime'],
          'lastSenderId': c['lastSenderId'],
          'unreadCount': c['unreadCount'],
        })).toList();
      } catch (e) {
        return <ChatModel>[];
      }
    });
  }

  static Stream<List<MessageModel>> getMessagesStream(String chatId) async* {
    // Initial fetch
    try {
      final res = await _client.dio.get('/chats/$chatId/messages');
      yield (res.data as List).map((m) => MessageModel.fromJson({
        'messageId': m['messageId'] ?? m['id'] ?? '',
        'senderId': m['senderId'],
        'content': m['content'],
        'timestamp': m['timestamp'],
        'messageType': m['messageType'],
        'photoUrl': m['photoUrl'],
        'isRead': m['isRead'],
      })).toList();
    } catch (e) {
      print('Initial getMessages error: $e');
    }

    // Polling loop
    yield* Stream.periodic(const Duration(seconds: 2)).asyncMap((_) async {
      try {
        final res = await _client.dio.get('/chats/$chatId/messages');
        return (res.data as List).map((m) => MessageModel.fromJson({
          'messageId': m['messageId'] ?? m['id'] ?? '',
          'senderId': m['senderId'],
          'content': m['content'],
          'timestamp': m['timestamp'],
          'messageType': m['messageType'],
          'photoUrl': m['photoUrl'],
          'isRead': m['isRead'],
        })).toList();
      } catch (e) {
        return <MessageModel>[];
      }
    });
  }

  static Future<void> sendMessage(String chatId, String content, {bool isPhoto = false}) async {
    if (isPhoto) {
      // Logic for sending photo message handled separately via multipart in typical real-world
      // Keeping this as text for now
      await _client.dio.post('/chats/$chatId/messages', data: {'content': content});
    } else {
      await _client.dio.post('/chats/$chatId/messages', data: {'content': content});
    }
  }

  static Future<void> markChatAsRead(String chatId, String userId) async {
    await _client.dio.put('/chats/$chatId/read');
  }

  static Future<List<ItemModel>> getItemsByIds(List<String> ids) async {
    if (ids.isEmpty) return [];
    // We fetch all items and filter locally for simplicity in this migration
    final res = await _client.dio.get('/items');
    final all = (res.data as List).map((i) => ItemModel.fromJson(i as Map<String, dynamic>)).toList();
    return all.where((i) => ids.contains(i.id)).toList();
  }

  static Future<void> removeFromSavedItems(String userId, String itemId) async {
    await _client.dio.delete('/saved/$itemId');
  }

  // ==================== ADDITIONAL MOCKS ====================
  static Future<void> toggleMfa(bool value) async {
    try {
      await _client.dio.post('/auth/mfa/toggle', data: {'enable': value});
    } catch(e) {}
  }
  
  static Future<void> blockUser(String chatId, String userId) async {
    await _client.dio.post('/chats/$chatId/block');
  }
  
  static Future<void> unblockUser(String chatId, String userId) async {
    await _client.dio.post('/chats/$chatId/unblock');
  }
  
  static Future<void> sendPhotoMessage(String chatId, dynamic photoFile) async {
    final formData = FormData.fromMap({
      'photo': await MultipartFile.fromFile(photoFile.path),
    });
    
    await _client.dio.post('/chats/$chatId/messages/photo', data: formData);
  }
  
  static Future<ExchangeModel?> getExchangeById(String exchangeId) async {
    try {
      final res = await _client.dio.get('/exchanges/$exchangeId');
      if (res.data != null) {
        return ExchangeModel.fromJson(res.data);
      }
      return null;
    } catch (e) {
      print('Failed to get exchange details: $e');
      return null;
    }
  }
  
  static Future<void> updateMeetingDetails(String exchangeId, String? location, DateTime? date) async {
    if (location != null && date != null) {
      await updateExchangeMeeting(exchangeId, location, date);
    }
  }
  
  static Future<void> submitReview({
    required String exchangeId,
    required String revieweeId,
    required double rating,
    required String comment,
  }) async {
    await submitExchangeRating(exchangeId, rating, comment);
  }
  
  static Future<void> createExchange({
    required String proposedTo,
    required List<ExchangeItem> itemsOffered,
    required List<ExchangeItem> itemsRequested,
    String? notes,
  }) async {
    await createExchangeProposal(ExchangeModel(
      id: '',
      proposedBy: _currentUser?.uid ?? '',
      proposedTo: proposedTo,
      itemsOffered: itemsOffered,
      itemsRequested: itemsRequested,
      meetingLocation: notes,
      proposedAt: DateTime.now(),
      status: ExchangeStatus.pending,
      chatId: '',
    ));
  }
  
  static Stream<int> getUnreadNotificationsCountStream(String userId) {
    return Stream.value(0);
  }
  
  static Stream<int> getTotalUnreadCountStream(String userId) {
    return Stream.value(0);
  }
  
  static Future<List<ItemModel>> getItemsForMap() async {
    return [];
  }
  
  static Future<List<ItemModel>> getItemsNearLocation({
    required double latitude,
    required double longitude,
    required double radiusKm,
    dynamic category,
    dynamic conditions,
  }) async {
    try {
      final queryParams = <String, dynamic>{
        'lat': latitude,
        'lng': longitude,
        'radius': radiusKm,
      };
      
      if (category != null && category is int) {
        queryParams['category'] = _mapCategoryToInt(category);
      } else if (category != null) {
        queryParams['category'] = _mapCategoryToInt(category);
      }

      final res = await _client.dio.get('/items/nearby', queryParameters: queryParams);
      var items = (res.data as List).map((i) => ItemModel.fromJson(i as Map<String, dynamic>)).toList();

      if (conditions != null && conditions is List && conditions.isNotEmpty) {
        items = items.where((i) => conditions.contains(i.condition)).toList();
      }

      return items;
    } catch (e) {
      print('Error getting nearby items: $e');
      return [];
    }
  }
  
  static List<ItemModel>? getCachedHomeItems() {
    return null;
  }
}

// ==================== FIREBASE MOCKS TO PREVENT UI ERRORS ====================
class FirebaseAuthException implements Exception {
  final String code;
  final String? message;
  FirebaseAuthException({required this.code, this.message});
}

class QuerySnapshot {
  List<dynamic> get docs => [];
}

class FirebaseFirestore {
  static final instance = _FirebaseFirestoreInstance();
}

class _FirebaseFirestoreInstance {
  _CollectionRef collection(String path) => _CollectionRef();
}

class _CollectionRef {
  _DocRef doc([String? path]) => _DocRef();
  _CollectionRef where(String field, {dynamic isEqualTo, dynamic arrayContains}) => this;
  Stream<QuerySnapshot> snapshots() => Stream.value(QuerySnapshot());
  Future<QuerySnapshot> get() async => QuerySnapshot();
}

class _DocRef {
  Future<void> set(Map<String, dynamic> data) async {}
  Future<void> update(Map<String, dynamic> data) async {}
}

