import 'package:flutter_test/flutter_test.dart';
import 'package:barter/model/item_model.dart';
import 'package:barter/services/api_service.dart';

void main() {
  test('Test addItemDirect', () async {
    try {
      final itemData = {
        'ownerId': 'test_uid',
        'ownerName': 'Test Owner',
        'title': 'Test Item',
        'description': 'Test Description',
        'imageUrls': ['https://via.placeholder.com/800'],
        'category': ItemCategory.electronics.index,
        'condition': ItemCondition.newItem.index,
        'preferredExchange': null,
        'location': 'Test Location',
        'latitude': 30.1,
        'longitude': 31.2,
        'detailedAddress': null,
        'createdAt': DateTime.now().toIso8601String(),
        'isAvailable': true,
        'itemType': ItemType.product.index,
        'isRemote': false,
      };
      
      await ApiService.addItemDirect(itemData);
      print('Success');
    } catch(e, s) {
      print('Error: $e');
      print(s);
      rethrow;
    }
  });
}
