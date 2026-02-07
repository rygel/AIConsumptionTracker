/// Masks sensitive content in strings (emails, account names)
pub fn mask_content(input: Option<&str>, account_name: Option<&str>) -> Option<String> {
    input.map(|s| {
        if s.is_empty() {
            return s.to_string();
        }

        // If account_name is provided and found in the string, mask it
        if let Some(name) = account_name {
            if s.contains(name) {
                return s.replace(name, &mask_string(name));
            }
        }

        // Find and mask any email in the string
        if s.contains('@') {
            return mask_emails_in_string(s);
        }

        // Mask the whole string
        mask_string(s)
    })
}

fn mask_emails_in_string(s: &str) -> String {
    // Simple email regex-like detection
    let mut result = s.to_string();
    let words: Vec<&str> = s.split_whitespace().collect();

    for word in words {
        if word.contains('@') && word.contains('.') {
            let masked = mask_single_email(word);
            result = result.replace(word, &masked);
        }
    }

    result
}

fn mask_single_email(email: &str) -> String {
    let parts: Vec<&str> = email.split('@').collect();
    if parts.len() != 2 {
        return mask_string(email);
    }

    let local = parts[0];
    let domain = parts[1];

    if local.len() <= 2 {
        format!("{}@{}", "*".repeat(local.len()), domain)
    } else {
        format!("{}...{}@{}", &local[..1], &local[local.len() - 1..], domain)
    }
}

fn mask_string(s: &str) -> String {
    if s.len() <= 1 {
        "*".to_string()
    } else if s.len() == 2 {
        "**".to_string()
    } else {
        format!(
            "{}{}{}",
            &s[..1],
            "*".repeat(s.len() - 2),
            &s[s.len() - 1..]
        )
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn mask_content_masks_email() {
        assert_eq!(
            mask_content(Some("test@example.com"), None),
            Some("t...t@example.com".to_string())
        );
    }

    #[test]
    fn mask_content_masks_long_email() {
        assert_eq!(
            mask_content(Some("alexander.brandt@gmail.com"), None),
            Some("a...t@gmail.com".to_string())
        );
    }

    #[test]
    fn mask_content_masks_with_account_name() {
        assert_eq!(
            mask_content(Some("johndoe"), Some("johndoe")),
            Some("j*****e".to_string())
        );
    }

    #[test]
    fn mask_content_masks_short_string() {
        assert_eq!(mask_content(Some("abc"), None), Some("a*c".to_string()));
    }

    #[test]
    fn mask_content_masks_two_char_string() {
        assert_eq!(mask_content(Some("ab"), None), Some("**".to_string()));
    }

    #[test]
    fn mask_content_masks_single_char() {
        assert_eq!(mask_content(Some("a"), None), Some("*".to_string()));
    }

    #[test]
    fn mask_content_handles_empty_string() {
        assert_eq!(mask_content(Some(""), None), Some("".to_string()));
    }

    #[test]
    fn mask_content_handles_none() {
        assert_eq!(mask_content(None, None), None);
    }

    #[test]
    fn mask_content_masks_surgically() {
        let input = "Logged in as johndoe";
        let result = mask_content(Some(input), Some("johndoe"));
        assert_eq!(result, Some("Logged in as j*****e".to_string()));
    }

    #[test]
    fn mask_content_masks_email_inside_string() {
        let input = "Usage for test@example.com is 50";
        let result = mask_content(Some(input), None);
        assert_eq!(
            result,
            Some("Usage for t...t@example.com is 50".to_string())
        );
    }
}
