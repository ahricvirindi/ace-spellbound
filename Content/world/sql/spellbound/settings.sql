UPDATE ace_shard_spellbound.config_properties_boolean SET `value` = 0 WHERE `key` = 'house_15day_account';
UPDATE ace_shard_spellbound.config_properties_boolean SET `value` = 0 WHERE `key` = 'house_hook_limit';
UPDATE ace_shard_spellbound.config_properties_boolean SET `value` = 0 WHERE `key` = 'house_hookgroup_limit';
UPDATE ace_shard_spellbound.config_properties_boolean SET `value` = 1 WHERE `key` = 'quest_info_enabled';

UPDATE ace_shard_spellbound.config_properties_long SET `value` = 4 WHERE `key` = 'default_subscription_level';
UPDATE ace_shard_spellbound.config_properties_long SET `value` = 20 WHERE `key` = 'max_chars_per_account';

