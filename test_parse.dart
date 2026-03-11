import 'dart:convert';
enum ItemCategory { electronics, clothing }
enum ItemCondition { new1, new2 }
enum ItemType { val1, val2 }

class ItemModel {
  ItemModel.fromJson(Map<String, dynamic> json) {
    var c = ItemCategory.values[json['category'] ?? 0];
  }
}
void main() {
  Map<String, dynamic> json = {
    'category': 'Clothing'
  };
  try {
    ItemModel.fromJson(json);
  } catch (e) {
    print(e);
  }
}
