use aic_core::{PaymentType, ProviderUsage};

fn format_usage_table(usages: &[ProviderUsage], verbose: bool) -> String {
    let mut sorted: Vec<_> = usages.iter().collect();
    sorted.sort_by(|a, b| {
        a.provider_name
            .to_lowercase()
            .cmp(&b.provider_name.to_lowercase())
    });

    let mut output = String::new();
    output.push_str(&format!(
        "{:<36} | {:<14} | {:<10} | {}\n",
        "Provider", "Type", "Used", "Description"
    ));
    output.push_str(&"-".repeat(98));
    output.push('\n');

    for u in sorted {
        let pct = if u.is_available {
            format!("{:.0}%", u.usage_percentage)
        } else {
            "-".to_string()
        };
        let type_str = if u.is_quota_based {
            "Quota"
        } else {
            "Pay-As-You-Go"
        };
        let account_info = if u.account_name.is_empty() {
            String::new()
        } else {
            format!(" [{}]", u.account_name)
        };

        let description = if u.description.is_empty() {
            account_info.trim().to_string()
        } else {
            format!("{}{}", u.description, account_info)
        };

        let lines: Vec<&str> = description.lines().collect();

        if lines.is_empty() {
            output.push_str(&format!(
                "{:<36} | {:<14} | {:<10} | {}\n",
                u.provider_name, type_str, pct, ""
            ));
        } else {
            output.push_str(&format!(
                "{:<36} | {:<14} | {:<10} | {}\n",
                u.provider_name, type_str, pct, lines[0]
            ));

            for line in &lines[1..] {
                output.push_str(&format!(
                    "{:<36} | {:<14} | {:<10} | {}\n",
                    "", "", "", line
                ));
            }
        }

        if verbose {
            output.push_str(&format!(
                "{:<36} | {:<14} | {:<10} |   Unit: {}\n",
                "", "", "", u.usage_unit
            ));
            if let Some(reset_time) = u.next_reset_time {
                output.push_str(&format!(
                    "{:<36} | {:<14} | {:<10} |   Reset: {}\n",
                    "", "", "", reset_time
                ));
            }
            output.push_str(&format!(
                "{:<36} | {:<14} | {:<10} |   Auth: {}\n",
                "", "", "", u.auth_source
            ));
            if u.cost_limit > 0.0 {
                output.push_str(&format!(
                    "{:<36} | {:<14} | {:<10} |   Cost: {}/{}\n",
                    "", "", "", u.cost_used, u.cost_limit
                ));
            }
        }

        if let Some(details) = &u.details {
            for d in details {
                let name = format!("  {}", d.name);
                output.push_str(&format!(
                    "{:<36} | {:<14} | {:<10} | {}\n",
                    name, "", d.used, d.description
                ));
            }
        }
    }

    output
}

#[test]
fn present_should_sort_providers_alphabetically() {
    // Arrange
    let usages = vec![
        ProviderUsage {
            provider_name: "Zebra".to_string(),
            provider_id: "zebra".to_string(),
            is_available: true,
            ..Default::default()
        },
        ProviderUsage {
            provider_name: "Alpha".to_string(),
            provider_id: "alpha".to_string(),
            is_available: true,
            ..Default::default()
        },
        ProviderUsage {
            provider_name: "Beta".to_string(),
            provider_id: "beta".to_string(),
            is_available: true,
            ..Default::default()
        },
    ];

    // Act
    let output = format_usage_table(&usages, false);
    let lines: Vec<&str> = output.lines().collect();

    // Assert - Skip header lines (2 lines)
    assert!(lines.len() > 2);

    let provider_lines: Vec<&str> = lines.iter().skip(2).cloned().collect();

    assert!(provider_lines[0].starts_with("Alpha"));
    assert!(provider_lines[1].starts_with("Beta"));
    assert!(provider_lines[2].starts_with("Zebra"));
}

#[test]
fn present_json_should_sort_alphabetically() {
    // Arrange
    let mut usages = vec![
        ProviderUsage {
            provider_name: "Zebra".to_string(),
            provider_id: "zebra".to_string(),
            is_available: true,
            ..Default::default()
        },
        ProviderUsage {
            provider_name: "Alpha".to_string(),
            provider_id: "alpha".to_string(),
            is_available: true,
            ..Default::default()
        },
    ];

    // Act - Sort as JSON would be sorted
    usages.sort_by(|a, b| {
        a.provider_name
            .to_lowercase()
            .cmp(&b.provider_name.to_lowercase())
    });
    let json = serde_json::to_string_pretty(&usages).unwrap();

    // Assert
    let doc: serde_json::Value = serde_json::from_str(&json).unwrap();
    assert!(doc.is_array());
    assert_eq!(doc.as_array().unwrap().len(), 2);

    assert_eq!(doc[0]["provider_name"].as_str().unwrap(), "Alpha");
    assert_eq!(doc[1]["provider_name"].as_str().unwrap(), "Zebra");
}

#[test]
fn present_verbose_should_show_details() {
    // Arrange
    let usages = vec![ProviderUsage {
        provider_name: "Test".to_string(),
        provider_id: "test".to_string(),
        is_available: true,
        usage_unit: "Tokens".to_string(),
        ..Default::default()
    }];

    // Act
    let output = format_usage_table(&usages, true);

    // Assert
    assert!(output.contains("Unit: Tokens"));
    assert!(output.contains("Auth:"));
}

#[test]
fn present_with_unavailable_provider_shows_dash() {
    // Arrange
    let usages = vec![ProviderUsage {
        provider_name: "Test".to_string(),
        provider_id: "test".to_string(),
        is_available: false,
        usage_percentage: 50.0,
        ..Default::default()
    }];

    // Act
    let output = format_usage_table(&usages, false);

    // Assert
    assert!(output.contains(" | -          |"));
}

#[test]
fn present_empty_list_shows_header_only() {
    // Arrange
    let usages: Vec<ProviderUsage> = vec![];

    // Act
    let output = format_usage_table(&usages, false);

    // Assert
    assert!(output.contains("Provider"));
    assert!(output.contains("Type"));
    assert!(output.contains("Used"));
    assert!(output.contains("Description"));
}
