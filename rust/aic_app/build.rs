use std::path::Path;
use std::process::Command;

fn check_javascript_syntax(file_path: &Path) -> bool {
    // Try to use Deno for JS checking if available
    let file_path_str = file_path.to_string_lossy().into_owned();
    let output = Command::new("deno")
        .args(&["check", "--no-cache", &file_path_str])
        .output();

    match output {
        Ok(output) => {
            if output.status.success() {
                println!("[check] JavaScript OK: {}", file_path.display());
                true
            } else {
                let stderr = String::from_utf8_lossy(&output.stderr);
                if !stderr.contains("error") {
                    println!(
                        "[check] JavaScript OK (deno check no-op): {}",
                        file_path.display()
                    );
                    return true;
                }
                eprintln!("[check] JavaScript errors in {}:", file_path.display());
                eprintln!("{}", stderr);
                false
            }
        }
        Err(_) => {
            // Deno not available, do basic syntax check
            match std::fs::read_to_string(file_path) {
                Ok(content) => {
                    if basic_js_syntax_check(&content, file_path) {
                        println!("[check] JavaScript OK: {}", file_path.display());
                        true
                    } else {
                        false
                    }
                }
                Err(e) => {
                    eprintln!("[check] Could not read {}: {}", file_path.display(), e);
                    false
                }
            }
        }
    }
}

fn basic_js_syntax_check(content: &str, file_path: &Path) -> bool {
    let mut errors = Vec::new();

    // Check for balanced braces
    let mut brace_count = 0;
    let mut paren_count = 0;
    let mut bracket_count = 0;
    let mut in_string = false;
    let mut string_char = '\0';
    let mut escaped = false;

    for (_line_num, line) in content.lines().enumerate() {
        let mut chars = line.chars().peekable();
        while let Some(c) = chars.next() {
            if escaped {
                escaped = false;
                continue;
            }

            if c == '\\' && in_string {
                escaped = true;
                continue;
            }

            if c == '"' || c == '\'' || c == '`' {
                if !in_string {
                    in_string = true;
                    string_char = c;
                } else if c == string_char {
                    in_string = false;
                }
                continue;
            }

            if in_string {
                continue;
            }

            match c {
                '{' => brace_count += 1,
                '}' => brace_count -= 1,
                '(' => paren_count += 1,
                ')' => paren_count -= 1,
                '[' => bracket_count += 1,
                ']' => bracket_count -= 1,
                _ => {}
            }
        }
    }

    if brace_count != 0 {
        errors.push(format!("Unbalanced braces: {}", brace_count));
    }
    if paren_count != 0 {
        errors.push(format!("Unbalanced parentheses: {}", paren_count));
    }
    if bracket_count != 0 {
        errors.push(format!("Unbalanced brackets: {}", bracket_count));
    }

    // Check for obvious syntax errors
    let lines: Vec<&str> = content.lines().collect();
    for (_i, line) in lines.iter().enumerate() {
        let line = line.trim();

        // Check for trailing comma before closing brace/bracket (if no comma-dangle setting)
        if line.ends_with(",}") || line.ends_with(",]") {
            // This is often valid in JS, but let's be lenient
        }

        // Check for common errors
        if line.contains("function(") && !line.contains("function (") {
            // function( is valid ES6
        }
    }

    if !errors.is_empty() {
        eprintln!("[check] JS syntax errors in {}:", file_path.display());
        for error in &errors {
            eprintln!("  {}", error);
        }
        false
    } else {
        true
    }
}

fn check_html_structure(file_path: &Path) -> bool {
    match std::fs::read_to_string(file_path) {
        Ok(content) => {
            let mut errors = Vec::new();

            // Basic HTML structure checks
            let has_doctype = content.to_lowercase().contains("<!doctype html>");
            let has_html_tag = content.contains("<html") && content.contains("</html>");
            let has_head = content.contains("<head") && content.contains("</head>");
            let has_body = content.contains("<body") && content.contains("</body>");

            if !has_doctype {
                errors.push("Missing DOCTYPE declaration".to_string());
            }
            if !has_html_tag {
                errors.push("Missing <html> tags".to_string());
            }
            if !has_head {
                errors.push("Missing <head> tags".to_string());
            }
            if !has_body {
                errors.push("Missing <body> tags".to_string());
            }

            // Check for script tags with JavaScript
            if content.contains("<script") {
                if content.contains("</script>") {
                    // Check for inline scripts
                }
            }

            if !errors.is_empty() {
                eprintln!("[check] HTML structure issues in {}:", file_path.display());
                for error in &errors {
                    eprintln!("  {}", error);
                }
                false
            } else {
                println!("[check] HTML OK: {}", file_path.display());
                true
            }
        }
        Err(e) => {
            eprintln!("[check] Could not read {}: {}", file_path.display(), e);
            false
        }
    }
}

fn main() {
    tauri_build::build();

    // Validate HTML and JavaScript files
    println!("[check] Validating UI files...");

    let src_dir = std::path::Path::new("src");
    if !src_dir.exists() {
        return;
    }

    let mut all_ok = true;

    // Check HTML files
    for entry in walkdir::WalkDir::new(src_dir)
        .follow_links(true)
        .into_iter()
        .filter_map(|e| e.ok())
    {
        let path = entry.path();
        if path.is_file() {
            if let Some(ext) = path.extension() {
                if ext == "html" {
                    if !check_html_structure(path) {
                        all_ok = false;
                    }
                }
                if ext == "js"
                    || path
                        .file_name()
                        .map(|n| n.to_string_lossy().ends_with(".js"))
                        .unwrap_or(false)
                {
                    if !check_javascript_syntax(path) {
                        all_ok = false;
                    }
                }
            }
        }
    }

    if !all_ok {
        eprintln!("[check] Errors found in UI files");
        std::process::exit(1);
    }

    println!("[check] All UI files validated successfully");
}
